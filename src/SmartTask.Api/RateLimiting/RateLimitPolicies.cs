namespace SmartTask.Api.RateLimiting;

/// <summary>Rate-limit policy names (API-CONVENTIONS §6).</summary>
public static class RateLimitPolicies
{
    /// <summary>Applied to every endpoint through the global limiter; partitioned by user id, otherwise by IP.</summary>
    public const string Global = "global";

    /// <summary>Register, login and refresh; partitioned by IP. Slows down credential stuffing.</summary>
    public const string Auth = "auth";

    /// <summary>The AI endpoint; partitioned by user id.</summary>
    public const string Ai = "ai";
}
