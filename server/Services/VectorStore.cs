// In-memory store of chunks + cosine-similarity search.
// Registered as a SINGLETON so indexed vectors survive between /upload and /ask.
public class VectorStore : IVectorStore
{
    private readonly List<Chunk> _chunks = new();

    public void Add(IEnumerable<Chunk> chunks) => _chunks.AddRange(chunks);
    public void Clear() => _chunks.Clear();

    public IReadOnlyList<SearchHit> Search(float[] queryVec, int topK = 4) =>
        _chunks.Select(c => new SearchHit(c, Cosine(c.Embedding, queryVec)))
               .OrderByDescending(h => h.Score)
               .Take(topK)
               .ToList();

    private static float Cosine(float[] a, float[] b)
    {
        float dot = 0, na = 0, nb = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na  += a[i] * a[i];
            nb  += b[i] * b[i];
        }
        return dot / (MathF.Sqrt(na) * MathF.Sqrt(nb));
    }
}
