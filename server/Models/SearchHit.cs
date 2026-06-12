// A retrieved chunk paired with its cosine-similarity score to the query.
// The score is computed by VectorStore (pure math) — it is what makes a
// citation provably real and per-question, not produced by the LLM.
public record SearchHit(Chunk Chunk, float Score);
