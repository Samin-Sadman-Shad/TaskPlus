namespace SmartTask.Application.Contracts.Ai;

/// <summary>
/// Port to a language-model provider (Strategy pattern): GitHub Models in production, a deterministic fake
/// without an API key or in tests. Implementations send the prepared prompt and return the raw reply;
/// parsing and validating it is the job of <c>AiService</c>, so every provider gets the same checks.
/// </summary>
public interface ITaskDescriptionImprover
{
    /// <exception cref="AiProviderException">The provider timed out, rate-limited, failed or returned nothing usable.</exception>
    /// <exception cref="OperationCanceledException">The caller cancelled the request.</exception>
    Task<ImprovedDescriptionResult> ImproveAsync(TaskDescriptionPromptInput input, CancellationToken cancellationToken = default);
}
