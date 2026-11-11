namespace SmartTask.Infrastructure.Ai;

/// <summary>Which provider was configured and which one is actually registered. Decided once, at startup.</summary>
public sealed record AiProviderSelection(AiProvider Configured, AiProvider Active)
{
    /// <summary>GitHub Models was configured but no key is set, so the fake provider answers.</summary>
    public bool FellBackToFake => Configured != Active;

    public static AiProviderSelection From(AiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new AiProviderSelection(options.Provider, options.UsesGitHubModels ? AiProvider.GitHubModels : AiProvider.Fake);
    }
}
