using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

// POST /ask — streams a grounded answer to the browser as Server-Sent Events.
// Emits: one {type:"sources"} event, many {type:"token"} events, then {type:"done"}.
[ApiController]
[Route("ask")]
public class AskController : ControllerBase
{
    // If even the best-matching chunk scores below this, nothing in the document
    // is relevant (e.g. a greeting) — so we don't surface citations at all. The
    // chunks are still used to ground the answer; we just don't show them.
    private const float MinCitationScore = 0.15f;

    private readonly IEmbeddingService _embed;
    private readonly IVectorStore _store;
    private readonly IChatService _chat;

    public AskController(IEmbeddingService embed, IVectorStore store, IChatService chat)
        => (_embed, _store, _chat) = (embed, store, chat);

    [HttpPost]
    public async Task Ask([FromBody] AskRequest req, CancellationToken ct)
    {
        IReadOnlyList<ChatTurn> history = req.History ?? Array.Empty<ChatTurn>();

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        // Don't buffer — flush each event to the client as it's written.
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        // Intent gate: is this actually a document question, or just chit-chat?
        // Chit-chat (greetings, thanks) skips retrieval entirely — no citations,
        // since the reply doesn't use the document.
        var isDocQuestion = await _chat.IsDocumentQuestion(req.Question, history, ct);

        try
        {
            if (!isDocQuestion)
            {
                await foreach (var token in _chat.ChatStream(req.Question, history, ct))
                    await Send(new { type = "token", text = token });
                await Send(new { type = "done" });
                return;
            }

            // For follow-ups, condense the question + history into a standalone search
            // query so retrieval finds the right chunks (matters on large documents).
            // Generation below still uses the ORIGINAL question + history.
            var searchQuery = history.Count > 0
                ? await _chat.RewriteQuery(req.Question, history, ct)
                : req.Question;

            var qVec = (await _embed.EmbedBatch(new[] { searchQuery }))[0];
            var hits = _store.Search(qVec, topK: 4);

            if (hits.Count == 0)
            {
                await Send(new { type = "error", message = "Upload a document first." });
                return;
            }

            // Citations are the exact chunks vector search returned for THIS question,
            // with their similarity scores. They come from retrieval (pure math here),
            // never from the LLM — so they cannot be hallucinated, and the scores
            // change per question. The same chunks ground the answer below.
            // Skip them when nothing in the document is relevant.
            if (hits[0].Score >= MinCitationScore)
            {
                await Send(new
                {
                    type = "sources",
                    sources = hits.Select(h => new
                    {
                        text = h.Chunk.Text,
                        score = MathF.Round(h.Score, 4)
                    }).ToArray()
                });
            }

            var chunks = hits.Select(h => h.Chunk).ToList();
            await foreach (var token in _chat.AnswerStream(req.Question, chunks, history, ct))
                await Send(new { type = "token", text = token });
        }
        catch (Exception ex)
        {
            await Send(new { type = "error", message = ex.Message });
        }

        await Send(new { type = "done" });

        // Write one SSE event: `data: {json}\n\n`, then flush.
        async Task Send(object data)
        {
            await Response.WriteAsync($"data: {JsonSerializer.Serialize(data)}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
    }
}
