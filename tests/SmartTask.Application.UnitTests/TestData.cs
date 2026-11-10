using Microsoft.Extensions.Time.Testing;

namespace SmartTask.Application.UnitTests;

internal static class TestData
{
    /// <summary>The fixed "today" (UTC) that time-dependent validators see through <see cref="Clock"/>.</summary>
    public static readonly DateOnly Today = new(2026, 6, 15);

    public const string ValidPassword = "Str0ng!Pass";

    public const string ValidEmail = "jane.doe@example.com";

    /// <summary>A clock at midday UTC on <see cref="Today"/>.</summary>
    public static FakeTimeProvider Clock() => new(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero));

    /// <summary>A string of letters of exactly <paramref name="length"/> characters.</summary>
    public static string Text(int length) => new('a', length);
}
