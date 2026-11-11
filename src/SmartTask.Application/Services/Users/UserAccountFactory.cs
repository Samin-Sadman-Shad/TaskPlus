using SmartTask.Application.Contracts.Identity;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;

namespace SmartTask.Application.Services.Users;

/// <summary>
/// Creates a new account with a hashed password and a role. Registration, admin-created users, the
/// bootstrap admin and the development seed all need the same create → hash → role sequence, so it
/// lives here once instead of four times.
/// </summary>
public static class UserAccountFactory
{
    /// <summary>
    /// <see cref="User.Create"/> needs a hash, and hashing needs the user instance. This placeholder is
    /// replaced before the method returns, so it is never saved.
    /// </summary>
    private const string PendingPasswordHash = "pending";

    public static User Create(
        string firstName,
        string lastName,
        string email,
        string password,
        UserRole role,
        IPasswordHasher passwordHasher,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(passwordHasher);
        ArgumentException.ThrowIfNullOrEmpty(password);

        var user = User.Create(firstName, lastName, email, PendingPasswordHash);
        user.ChangePasswordHash(passwordHasher.Hash(user, password), now);
        user.ChangeRole(role);
        return user;
    }
}
