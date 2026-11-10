using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using SmartTask.Infrastructure.Identity;

namespace SmartTask.Infrastructure.UnitTests;

internal static class TestJwt
{
    /// <summary>A test-only key (≥ 32 bytes). Never used outside tests.</summary>
    public const string SigningKey = "test-signing-key-0123456789-abcdefghijklmnopqrstuvwxyz";

    public static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    public static JwtOptions Options(Action<JwtOptions>? configure = null)
    {
        var options = new JwtOptions
        {
            Issuer = "SmartTask.Tests",
            Audience = "SmartTask.Tests.Client",
            SigningKey = SigningKey,
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7,
        };
        configure?.Invoke(options);
        return options;
    }

    public static IOptions<JwtOptions> Wrap(JwtOptions options) => Microsoft.Extensions.Options.Options.Create(options);

    public static FakeTimeProvider Clock() => new(Now);
}
