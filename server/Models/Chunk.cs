// A single piece of the uploaded PDF, plus its embedding vector.
public record Chunk(string Text, float[] Embedding, int Page);
