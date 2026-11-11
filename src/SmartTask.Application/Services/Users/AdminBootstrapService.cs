using FluentValidation;
using Microsoft.Extensions.Logging;
using SmartTask.Application.Common.Exceptions;
using SmartTask.Application.Contracts.Identity;
using SmartTask.Application.Contracts.Persistence;
using SmartTask.Application.Contracts.Services;
using SmartTask.Application.DTOs.Users;
using SmartTask.Domain.Enums;

namespace SmartTask.Application.Services.Users;

/// <inheritdoc cref="IAdminBootstrapService" />
/// <remarks>
/// The settings are checked with <see cref="Validators.Users.CreateUserDtoValidator"/>, so the bootstrap
/// admin obeys the same name, email and password policy as any admin-created user. Log messages carry
/// the user id only, never the email or password.
/// </remarks>
public sealed class AdminBootstrapService(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IValidator<CreateUserDto> validator,
    TimeProvider time,
    ILogger<AdminBootstrapService> logger) : IAdminBootstrapService
{
    public async Task<AdminBootstrapOutcome> EnsureAdminAsync(CreateUserDto admin, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(admin);

        var request = admin with { Role = UserRole.Admin, ConfirmPassword = admin.Password };
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            // Messages name the setting and the rule; they never include the value.
            var problems = string.Join("; ", validation.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"));
            throw new InvalidOperationException($"The Bootstrap:Admin settings are invalid. {problems}");
        }

        if (await users.ActiveUserWithRoleExistsAsync(UserRole.Admin, cancellationToken))
        {
            logger.LogInformation("Admin bootstrap skipped: an active admin already exists");
            return AdminBootstrapOutcome.AdminAlreadyExists;
        }

        if (await users.EmailExistsAsync(request.Email, cancellationToken))
        {
            logger.LogWarning(
                "Admin bootstrap skipped: no active admin exists, and the configured email belongs to an existing non-admin account. " +
                "It is not promoted automatically; configure a different email or promote the account manually.");
            return AdminBootstrapOutcome.EmailBelongsToExistingAccount;
        }

        var user = UserAccountFactory.Create(
            request.FirstName, request.LastName, request.Email, request.Password, UserRole.Admin, passwordHasher, time.GetUtcNow());
        await users.AddAsync(user, cancellationToken);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConflictException)
        {
            // Another instance starting at the same time created it first.
            logger.LogInformation("Admin bootstrap skipped: another instance created the admin first");
            return AdminBootstrapOutcome.AdminAlreadyExists;
        }

        logger.LogInformation("Bootstrap admin {UserId} created", user.Id);
        return AdminBootstrapOutcome.Created;
    }
}
