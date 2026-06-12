// Contract for generating a grounded answer, streamed token-by-token.
// `history` is the prior conversation so follow-up questions keep context.
public interface IChatService
{
    // Grounded, streamed answer for a document question.
    IAsyncEnumerable<string> AnswerStream(
        string question, List<Chunk> chunks,
        IReadOnlyList<ChatTurn> history, CancellationToken ct = default);

    // Plain conversational reply for chit-chat (no retrieval, no citations).
    IAsyncEnumerable<string> ChatStream(
        string question, IReadOnlyList<ChatTurn> history, CancellationToken ct = default);

    // Is the latest message actually about the document, or just chit-chat?
    Task<bool> IsDocumentQuestion(
        string question, IReadOnlyList<ChatTurn> history, CancellationToken ct = default);

    // Condense a follow-up + history into one standalone search query for
    // retrieval (e.g. "another one" -> "another git command besides git commit").
    Task<string> RewriteQuery(
        string question, IReadOnlyList<ChatTurn> history, CancellationToken ct = default);
}
