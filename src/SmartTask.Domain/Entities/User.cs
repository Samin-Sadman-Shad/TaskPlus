using SmartTask.Domain.Common;
using SmartTask.Domain.Constants;
using SmartTask.Domain.Enums;
using SmartTask.Domain.Exceptions;

namespace SmartTask.Domain.Entities;

/// <summary>
/// An application user. Self-registered users are always <see cref="UserRole.TeamMember"/>;
/// only an admin changes roles.
/// </summary>
public sealed class User : AuditableEntity
{
    private const string PasswordChangedReason = "Password changed";

    private readonly List<RefreshToken> _refreshTokens = [];

    private User()
    {
    }

    public string FirstName { get; private set; } = null!;

    public string LastName { get; private set; } = null!;

    public string Email { get; private set; } = null!;

    public string NormalizedEmail { get; private set; } = null!;

    public string PasswordHash { get; private set; } = null!;

    public UserRole Role { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    public string FullName => $"{FirstName} {LastName}";

    /// <summary>Creates an active <see cref="UserRole.TeamMember"/> with a trimmed, normalised email.</summary>
    public static User Create(string firstName, string lastName, string email, string passwordHash)
    {
        var user = new User
        {
            Role = UserRole.TeamMember,
            IsActive = true,
        };

        user.SetName(firstName, lastName);
        user.SetEmail(email);
        user.SetPasswordHash(passwordHash);

        return user;
    }

    public void UpdateProfile(string firstName, string lastName) => SetName(firstName, lastName);

    public void ChangeRole(UserRole role) => Role = Guard.DefinedEnum(role, nameof(Role));

    /// <summary>
    /// Replaces the password hash and revokes every active refresh token, so existing sessions
    /// must sign in again with the new password.
    /// </summary>
    public void ChangePasswordHash(string newHash, DateTimeOffset now)
    {
        SetPasswordHash(newHash);
        RevokeAllRefreshTokens(now, PasswordChangedReason);
    }

    /// <summary>
    /// Replaces the stored hash with a stronger hash of the <b>same</b> password (for example after the
    /// hashing algorithm's work factor was raised). Unlike <see cref="ChangePasswordHash"/>, it keeps
    /// existing sessions, because the user's credentials did not change.
    /// </summary>
    public void RehashPassword(string newHash) => SetPasswordHash(newHash);

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public void RecordLogin(DateTimeOffset at) => LastLoginAt = at;

    /// <exception cref="DomainException">The token was issued for a different user.</exception>
    public void AddRefreshToken(RefreshToken token)
    {
        ArgumentNullException.ThrowIfNull(token);

        if (token.UserId != Id)
        {
            throw new DomainException("Refresh token belongs to a different user.");
        }

        _refreshTokens.Add(token);
    }

    /// <summary>Revokes every token that is still active at <paramref name="at"/>.</summary>
    public void RevokeAllRefreshTokens(DateTimeOffset at, string reason)
    {
        foreach (var token in _refreshTokens.Where(t => t.IsActive(at)))
        {
            token.Revoke(at, reason);
        }
    }

    private void SetName(string firstName, string lastName)
    {
        FirstName = Guard.RequiredText(firstName, nameof(FirstName), UserConstraints.FirstNameMaxLength);
        LastName = Guard.RequiredText(lastName, nameof(LastName), UserConstraints.LastNameMaxLength);
    }

    private void SetEmail(string email)
    {
        Email = Guard.RequiredText(email, nameof(Email), UserConstraints.EmailMaxLength);
        NormalizedEmail = Email.ToUpperInvariant();
    }

    private void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("Password hash is required.");
        }

        PasswordHash = passwordHash;
    }
}
