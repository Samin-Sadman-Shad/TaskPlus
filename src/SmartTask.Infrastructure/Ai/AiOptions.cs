namespace SmartTask.Infrastructure.Ai;

/// <summary>
/// Bound from the <c>Ai</c> section. <see cref="ApiKey"/> comes from user-secrets or the <c>Ai__ApiKey</c>
/// environment variable, never from a config file. Validated on startup by <see cref="AiOptionsValidator"/>.
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public AiProvider Provider { get; set; } = AiProvider.GitHubModels;

    /// <summary>Base URL of an OpenAI-compatible API; chat completions are posted to <c>{Endpoint}/chat/completions</c>.</summary>
    public string Endpoint { get; set; } = "https://models.github.ai/inference";

    public string Model { get; set; } = "openai/gpt-4.1-mini";

    /// <summary>For GitHub Models: a fine-grained personal access token with the <c>models:read</c> permission.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Upper bound for one call, retries included.</summary>
    public int TimeoutSeconds { get; set; } = 20;

    public int MaxOutputTokens { get; set; } = 600;

    /// <summary>Low, so rewrites stay close to the input and repeat calls give similar results.</summary>
    public float Temperature { get; set; } = 0.3f;

    /// <summary>Whether the real provider can be used: GitHub Models is selected and a key is present.</summary>
    public bool UsesGitHubModels => Provider == AiProvider.GitHubModels && !string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>Never prints the key.</summary>
    public override string ToString() =>
        $"{nameof(AiOptions)} {{ Provider = {Provider}, Endpoint = {Endpoint}, Model = {Model}, ApiKey = {(string.IsNullOrEmpty(ApiKey) ? "(empty)" : "***")} }}";
}
