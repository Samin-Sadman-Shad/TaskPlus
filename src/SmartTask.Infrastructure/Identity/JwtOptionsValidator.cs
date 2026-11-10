using System.Text;
using Microsoft.Extensions.Options;

namespace SmartTask.Infrastructure.Identity;

/// <summary>
/// Runs at startup (<c>ValidateOnStart</c>), so a missing or weak signing key stops the app instead of
/// failing on the first login. Messages name the setting, never its value.
/// </summary>
public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    private const int MaxAccessTokenMinutes = 60;
    private const int MaxRefreshTokenDays = 90;

    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add("Jwt:Issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("Jwt:Audience is required.");
        }

        if (Encoding.UTF8.GetByteCount(options.SigningKey) < JwtOptions.MinSigningKeyBytes)
        {
            failures.Add(
                $"Jwt:SigningKey must be at least {JwtOptions.MinSigningKeyBytes} bytes. Set it with " +
                "'dotnet user-secrets set \"Jwt:SigningKey\" \"<64+ random characters>\" -p src/SmartTask.Api' " +
                "or the Jwt__SigningKey environment variable.");
        }

        if (options.AccessTokenMinutes is < 1 or > MaxAccessTokenMinutes)
        {
            failures.Add($"Jwt:AccessTokenMinutes must be between 1 and {MaxAccessTokenMinutes}.");
        }

        if (options.RefreshTokenDays is < 1 or > MaxRefreshTokenDays)
        {
            failures.Add($"Jwt:RefreshTokenDays must be between 1 and {MaxRefreshTokenDays}.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
