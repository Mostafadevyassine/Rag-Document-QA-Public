using System.Net.Http.Json;

// Calls OpenAI embeddings. Embeds BOTH document chunks and the question
// with the same model so the vectors are comparable.
public class EmbeddingService : IEmbeddingService
{
    private readonly HttpClient _http;
    public EmbeddingService(HttpClient http) => _http = http;
    // base address + Authorization: Bearer {OPENAI_API_KEY} set at registration

    public async Task<float[][]> EmbedBatch(IEnumerable<string> texts)
    {
        var req = new { input = texts, model = "text-embedding-3-small" };
        var resp = await _http.PostAsJsonAsync("v1/embeddings", req);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<EmbeddingResponse>();
        return json!.data.Select(d => d.embedding).ToArray();
    }

    private record EmbeddingResponse(List<EmbeddingItem> data);
    private record EmbeddingItem(float[] embedding);
}
