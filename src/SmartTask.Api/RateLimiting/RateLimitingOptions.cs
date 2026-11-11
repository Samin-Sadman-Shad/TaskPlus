namespace SmartTask.Api.RateLimiting;

/// <summary>Limits bound from the <c>RateLimiting</c> configuration section. The defaults match API-CONVENTIONS §6.</summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Fixed window per user (or IP): 100 requests per minute.</summary>
    public WindowLimit Global { get; set; } = new() { PermitLimit = 100, WindowSeconds = 60 };

    /// <summary>Sliding window per IP: 20 requests per minute.</summary>
    public WindowLimit Auth { get; set; } = new() { PermitLimit = 20, WindowSeconds = 60, SegmentsPerWindow = 6 };

    /// <summary>Token bucket per user: 10 requests per minute.</summary>
    public TokenBucketLimit Ai { get; set; } = new() { TokenLimit = 10, TokensPerPeriod = 10, ReplenishmentSeconds = 60 };

    public sealed class WindowLimit
    {
        public int PermitLimit { get; set; }

        public int WindowSeconds { get; set; }

        /// <summary>For sliding windows only.</summary>
        public int SegmentsPerWindow { get; set; } = 1;
    }

    public sealed class TokenBucketLimit
    {
        public int TokenLimit { get; set; }

        public int TokensPerPeriod { get; set; }

        public int ReplenishmentSeconds { get; set; }
    }
}
