using Microsoft.AspNetCore.Identity;
using SmartTask.Application.Contracts.Identity;
using SmartTask.Domain.Entities;

namespace SmartTask.Infrastructure.Identity;

/// <summary>
/// Adapter from ASP.NET Core Identity's <see cref="PasswordHasher{TUser}"/> (PBKDF2 with HMAC-SHA512,
/// random salt, high iteration count) to the Application's <see cref="IPasswordHasher"/>. It reports
/// when a hash uses outdated parameters, so login can rehash it.
/// </summary>
public sealed class PasswordHasherAdapter : IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(User user, string password)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrEmpty(password);

        return _hasher.HashPassword(user, password);
    }

    public PasswordCheckResult Verify(User user, string hash, string password)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(password))
        {
            return PasswordCheckResult.Failed;
        }

        try
        {
            return _hasher.VerifyHashedPassword(user, hash, password) switch
            {
                PasswordVerificationResult.Success => PasswordCheckResult.Success,
                PasswordVerificationResult.SuccessRehashNeeded => PasswordCheckResult.SuccessRehashNeeded,
                _ => PasswordCheckResult.Failed,
            };
        }
        catch (FormatException)
        {
            // A stored hash that isn't valid Base64 can never match.
            return PasswordCheckResult.Failed;
        }
    }
}
