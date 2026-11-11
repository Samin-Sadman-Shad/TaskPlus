namespace SmartTask.Application.Contracts.Ai;

/// <summary>
/// The only exception an <see cref="ITaskDescriptionImprover"/> throws for provider failures. The message
/// is ours, never the provider's, so nothing from the provider can reach a client. The original exception
/// is kept as <see cref="Exception.InnerException"/> for server-side logs.
/// </summary>
/// <remarks>
/// It lives in Application (not Infrastructure) because <c>AiService</c> catches it, and Application may
/// not reference Infrastructure.
/// </remarks>
public sealed class AiProviderException : Exception
{
    public AiProviderException(AiFailureReason reason, Exception? innerException = null)
        : base($"The AI provider call failed: {reason}.", innerException)
    {
        Reason = reason;
    }

    public AiFailureReason Reason { get; }
}
