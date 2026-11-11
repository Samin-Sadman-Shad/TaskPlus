using SmartTask.Application.DTOs.Users;

namespace SmartTask.Application.Contracts.Services;

/// <summary>What <see cref="IAdminBootstrapService.EnsureAdminAsync"/> did.</summary>
public enum AdminBootstrapOutcome
{
    /// <summary>No active admin existed, so the configured admin was created.</summary>
    Created = 1,

    /// <summary>An active admin already exists; nothing was changed (passwords are never overwritten).</summary>
    AdminAlreadyExists = 2,

    /// <summary>
    /// No active admin exists, but a non-admin account already uses the configured email. It is
    /// <b>not</b> promoted: anyone can self-register, so promoting by email would hand admin rights to
    /// whoever registered that address first.
    /// </summary>
    EmailBelongsToExistingAccount = 3,
}

/// <summary>
/// Solves the first-admin problem: self-registration only creates team members, and only an admin can
/// change roles or create other admins. On start-up the host calls this with the deployment's
/// configured admin, so a fresh database always gets exactly one way in.
/// </summary>
public interface IAdminBootstrapService
{
    /// <summary>
    /// Creates <paramref name="admin"/> as an Admin when no active admin exists. Idempotent; safe on every start.
    /// <c>Role</c> and <c>ConfirmPassword</c> on <paramref name="admin"/> are ignored.
    /// </summary>
    /// <exception cref="InvalidOperationException">The settings fail the same rules as <c>POST users</c>.</exception>
    Task<AdminBootstrapOutcome> EnsureAdminAsync(CreateUserDto admin, CancellationToken cancellationToken = default);
}
