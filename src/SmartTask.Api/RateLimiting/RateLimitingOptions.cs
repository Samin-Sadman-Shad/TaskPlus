namespace SmartTask.Api.RateLimiting;

/// <summary>Limits bound from the <c>RateLimiting</c> configuration section. The defaults match API-CONVENTIONS §6.</summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public WindowLimit Global { get; set; } = new() { PermitLimit = 100, WindowSeconds = 60 };

    public WindowLimit Auth { get; set; } = new() { PermitLimit = 5, WindowSeconds = 60, SegmentsPerWindow = 6 };

    public sealed class WindowLimit
    {
        public int PermitLimit { get; set; }

        public int WindowSeconds { get; set; }

        /// <summary>For sliding windows only.</summary>
        public int SegmentsPerWindow { get; set; } = 1;
    }
}
