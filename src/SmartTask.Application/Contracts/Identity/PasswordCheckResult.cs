namespace SmartTask.Application.Contracts.Identity;

/// <summary>
/// Result of <see cref="IPasswordHasher.Verify"/>. It is our own enum, so the Application layer doesn't
/// depend on (or clash with) ASP.NET Core Identity's <c>PasswordVerificationResult</c>.
/// </summary>
public enum PasswordCheckResult
{
    Failed = 1,
    Success = 2,

    /// <summary>The password is correct, but the hash uses outdated parameters and should be replaced.</summary>
    SuccessRehashNeeded = 3,
}
