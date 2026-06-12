// Strongly-typed OpenAI settings. The API key comes from user-secrets / env vars
// (never committed); model names and base url live here to kill magic strings.
public sealed class OpenAiOptions
{
    public required string ApiKey { get; init; }
    public string EmbeddingModel { get; init; } = "text-embedding-3-small";
    public string ChatModel { get; init; } = "gpt-4o-mini";
    public string BaseUrl { get; init; } = "https://api.openai.com/";
}
