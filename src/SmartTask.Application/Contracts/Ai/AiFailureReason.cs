namespace SmartTask.Application.Contracts.Ai;

/// <summary>Why a provider call failed. <c>AiService</c> maps each reason to a status code (API-CONVENTIONS §2).</summary>
public enum AiFailureReason
{
    /// <summary>No answer within <c>Ai:TimeoutSeconds</c> → 503.</summary>
    Timeout,

    /// <summary>The provider returned 429 after its retries → 503 "busy".</summary>
    RateLimited,

    /// <summary>The provider answered, but not with usable content → 502.</summary>
    InvalidResponse,

    /// <summary>Network error, 5xx, or a rejected key / model → 503.</summary>
    Unavailable,
}
