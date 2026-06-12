// Contract for the in-memory vector store + cosine-similarity search.
public interface IVectorStore
{
    void Add(IEnumerable<Chunk> chunks);
    void Clear();
    // Returns the top-K chunks with their cosine-similarity scores.
    IReadOnlyList<SearchHit> Search(float[] queryVec, int topK = 4);
}
