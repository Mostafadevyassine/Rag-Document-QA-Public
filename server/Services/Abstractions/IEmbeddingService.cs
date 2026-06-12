// Contract for turning text into embedding vectors (so it can be faked in tests).
public interface IEmbeddingService
{
    Task<float[][]> EmbedBatch(IEnumerable<string> texts);
}
