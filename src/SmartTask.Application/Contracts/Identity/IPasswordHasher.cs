using SmartTask.Domain.Entities;

namespace SmartTask.Application.Contracts.Identity;

/// <summary>Hashes and verifies passwords. The implementation wraps ASP.NET Core Identity's <c>PasswordHasher&lt;User&gt;</c> (PBKDF2).</summary>
public interface IPasswordHasher
{
    string Hash(User user, string password);

    PasswordCheckResult Verify(User user, string hash, string password);
}
