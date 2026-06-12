using Microsoft.AspNetCore.Mvc;

// POST /ask/bot — the same grounded RAG pipeline as /ask, but returns the whole
// answer as ONE JSON object instead of a token stream. External automations
// (n8n -> WhatsApp) call a backend once and get a single reply; they can't
// consume Server-Sent Events, and a chat message is atomic anyway.
//
// Guarded by a shared secret in the `X-Bot-Secret` header so the public n8n
// tunnel doesn't expose retrieval+generation to anyone who finds the URL.
[ApiController]
[Route("ask/bot")]
public class BotAskController : ControllerBase
{
    // Same floor as AskController: below this, nothing in the document is
    // relevant (e.g. a greeting), so we omit citations.
    private const float MinCitationScore = 0.15f;

    private readonly IEmbeddingService _embed;
    private readonly IVectorStore _store;
    private readonly IChatService _chat;
    private readonly string? _secret;

    public BotAskController(
        IEmbeddingService embed, IVectorStore store, IChatService chat, IConfiguration config)
    {
        _embed = embed;
        _store = store;
        _chat = chat;
        _secret = config["BOT_SHARED_SECRET"];
    }

    [HttpPost]
    public async Task<IActionResult> Ask([FromBody] AskRequest req, CancellationToken ct)
    {
        // Reject unless the caller presents the shared secret (when one is set).
        if (!string.IsNullOrEmpty(_secret) &&
            Request.Headers["X-Bot-Secret"] != _secret)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(req.Question))
            return BadRequest(new { error = "Question is required." });

        IReadOnlyList<ChatTurn> history = req.History ?? Array.Empty<ChatTurn>();

        // Intent gate: a real document question, or just chit-chat?
        var isDocQuestion = await _chat.IsDocumentQuestion(req.Question, history, ct);

        if (!isDocQuestion)
        {
            var chat = await Collect(_chat.ChatStream(req.Question, history, ct));
            return Ok(new BotAskResponse(chat, Array.Empty<BotSource>()));
        }

        // Condense follow-ups into a standalone search query (generation still
        // uses the original question + history).
        var searchQuery = history.Count > 0
            ? await _chat.RewriteQuery(req.Question, history, ct)
            : req.Question;

        var qVec = (await _embed.EmbedBatch(new[] { searchQuery }))[0];
        var hits = _store.Search(qVec, topK: 4);

        if (hits.Count == 0)
            return Ok(new BotAskResponse(
                "No document has been uploaded yet. Upload a PDF first, then ask.",
                Array.Empty<BotSource>()));

        var sources = hits[0].Score >= MinCitationScore
            ? hits.Select(h => new BotSource(h.Chunk.Text, MathF.Round(h.Score, 4))).ToArray()
            : Array.Empty<BotSource>();

        var chunks = hits.Select(h => h.Chunk).ToList();
        var answer = await Collect(_chat.AnswerStream(req.Question, chunks, history, ct));

        return Ok(new BotAskResponse(answer, sources));
    }

    // Drain a token stream into one string.
    private static async Task<string> Collect(IAsyncEnumerable<string> tokens)
    {
        var sb = new System.Text.StringBuilder();
        await foreach (var t in tokens) sb.Append(t);
        return sb.ToString();
    }
}
