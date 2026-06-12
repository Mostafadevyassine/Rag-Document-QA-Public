using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;

// Talks to the OpenAI chat model: a grounded streamed answer for document
// questions, a plain conversational reply for chit-chat, plus two small helper
// calls (classify intent, rewrite follow-ups into standalone search queries).
public class ChatService : IChatService
{
    private readonly HttpClient _http;
    public ChatService(HttpClient http) => _http = http;
    // base address + Authorization: Bearer {OPENAI_API_KEY} set at registration

    // Grounded answer: system (prompt + retrieved chunks) -> history -> question.
    public IAsyncEnumerable<string> AnswerStream(
        string question, List<Chunk> chunks, IReadOnlyList<ChatTurn> history,
        CancellationToken ct = default)
    {
        var context = string.Join("\n---\n", chunks.Select(c => c.Text));
        var system =
            "You are a helpful assistant answering questions about a document the " +
            "user uploaded. Use ONLY the information in the context below — do not " +
            "rely on outside knowledge. You may summarize, list, explain, or draw " +
            "reasonable conclusions from what the context contains. Earlier turns of " +
            "the conversation are included so you can resolve follow-up questions " +
            "(e.g. \"another one\").\n\n" +
            "If the context does not contain the answer, do NOT just say \"I don't " +
            "know.\" Instead, briefly tell the user that this document doesn't cover " +
            "the topic they asked about, and—based only on the context—say in one " +
            "sentence what the document actually IS about, so they know what they can " +
            "ask. Never invent facts that aren't in the context.\n\nContext:\n" +
            context;

        return StreamCompletion(BuildMessages(system, history, question), ct);
    }

    // Conversational reply for non-document turns (greetings, small talk) — no
    // retrieval, no grounding, no citations.
    public IAsyncEnumerable<string> ChatStream(
        string question, IReadOnlyList<ChatTurn> history, CancellationToken ct = default)
    {
        var system =
            "You are a friendly assistant for a document Q&A app. The user's latest " +
            "message is conversational (a greeting, thanks, or small talk), not a " +
            "question about their document. Reply briefly and warmly, and invite " +
            "them to ask something about the document.";

        return StreamCompletion(BuildMessages(system, history, question), ct);
    }

    // Should this turn use the document (retrieve + cite), or is it pure chit-chat?
    // Biased toward "yes": only obvious social niceties are filtered out, so we
    // never drop a real question's grounding.
    public async Task<bool> IsDocumentQuestion(
        string question, IReadOnlyList<ChatTurn> history, CancellationToken ct = default)
    {
        var system =
            "You label the user's latest message. Reply \"no\" ONLY if it is pure " +
            "social chit-chat with no information request — e.g. a greeting (\"hello\", " +
            "\"hi\"), thanks, goodbye, or small talk like \"how are you\". For ANY " +
            "message that asks a question or requests information of any kind — even a " +
            "general one — reply \"yes\". When unsure, reply \"yes\". Reply with " +
            "exactly \"yes\" or \"no\".";
        var messages = BuildMessages(system, history,
            $"Latest message: \"{question}\"\n\nyes or no:");

        var payload = new { model = "gpt-4o-mini", messages, max_tokens = 1, temperature = 0 };
        var resp = await _http.PostAsJsonAsync("v1/chat/completions", payload, ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var text = json.GetProperty("choices")[0].GetProperty("message")
                       .GetProperty("content").GetString();
        // Fail open: on anything unexpected, treat it as a document question.
        return text?.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase) ?? true;
    }

    public async Task<string> RewriteQuery(
        string question, IReadOnlyList<ChatTurn> history, CancellationToken ct = default)
    {
        var system =
            "You rewrite the user's latest message into a single standalone search " +
            "query for retrieving passages from a document. Use the conversation to " +
            "resolve references like \"another one\", \"it\", or \"that\". Reply with " +
            "ONLY the query text — no quotes, no explanation.";
        var messages = BuildMessages(system, history,
            $"Latest message: \"{question}\"\n\nStandalone search query:");

        // Non-streaming, deterministic, short — this is a cheap retrieval helper.
        var payload = new { model = "gpt-4o-mini", messages, max_tokens = 64, temperature = 0 };
        var resp = await _http.PostAsJsonAsync("v1/chat/completions", payload, ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var text = json.GetProperty("choices")[0].GetProperty("message")
                       .GetProperty("content").GetString();
        var rewritten = text?.Trim().Trim('"');
        return string.IsNullOrWhiteSpace(rewritten) ? question : rewritten;
    }

    // system -> prior conversation turns -> the current user message.
    private static List<object> BuildMessages(
        string system, IReadOnlyList<ChatTurn> history, string userMessage)
    {
        var messages = new List<object> { new { role = "system", content = system } };
        foreach (var turn in history)
            messages.Add(new { role = turn.Role == "assistant" ? "assistant" : "user",
                               content = turn.Content });
        messages.Add(new { role = "user", content = userMessage });
        return messages;
    }

    // Stream an OpenAI chat completion, yielding each token's text as it arrives.
    private async IAsyncEnumerable<string> StreamCompletion(
        List<object> messages, [EnumeratorCancellation] CancellationToken ct)
    {
        var payload = new { model = "gpt-4o-mini", stream = true, messages };
        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions")
        {
            Content = JsonContent.Create(payload)
        };
        // ResponseHeadersRead = don't buffer the whole body; read as it streams.
        using var resp = await _http.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        // OpenAI streams lines like: data: {json}  ... ending with: data: [DONE]
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;
            if (!line.StartsWith("data:")) continue;

            var data = line[5..].TrimStart();
            if (data == "[DONE]") break;

            var token = ExtractToken(data);
            if (!string.IsNullOrEmpty(token)) yield return token;
        }
    }

    // Pull choices[0].delta.content out of one streamed chunk.
    private static string? ExtractToken(string data)
    {
        try
        {
            using var doc = JsonDocument.Parse(data);
            var choices = doc.RootElement.GetProperty("choices");
            if (choices.GetArrayLength() == 0) return null;
            var delta = choices[0].GetProperty("delta");
            return delta.TryGetProperty("content", out var c)
                   && c.ValueKind == JsonValueKind.String
                ? c.GetString()
                : null;
        }
        catch
        {
            return null;   // ignore keep-alives / malformed lines
        }
    }
}
