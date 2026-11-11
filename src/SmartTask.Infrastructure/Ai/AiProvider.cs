namespace SmartTask.Infrastructure.Ai;

/// <summary>Value of <c>Ai:Provider</c>.</summary>
public enum AiProvider
{
    /// <summary>GitHub Models (OpenAI-compatible chat completions). Needs <c>Ai:ApiKey</c>.</summary>
    GitHubModels,

    /// <summary>Deterministic, offline. Used when no key is configured and in tests.</summary>
    Fake,
}
