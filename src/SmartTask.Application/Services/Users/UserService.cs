using Microsoft.Extensions.Logging;
using SmartTask.Application.Common.Exceptions;
using SmartTask.Application.Common.Models;
using SmartTask.Application.Common.Responses;
using SmartTask.Application.Contracts.Identity;
using SmartTask.Application.Contracts.Persistence;
using SmartTask.Application.Contracts.Services;
using SmartTask.Application.DTOs.Users;
using SmartTask.Application.Services.Authorization;
using SmartTask.Domain.Constants;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;

namespace SmartTask.Application.Services.Users;

/// <remarks>
/// The admin-only endpoints are also protected by <c>[Authorize(Roles = AppRoles.Admin)]</c> in the API. The checks
/// here are defence in depth, so the service is safe whatever calls it.
/// </remarks>
public sealed class UserService(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IProjectAccessGuard access,
    ICurrentUserService currentUser,
    IPasswordHasher passwordHasher,
    TimeProvider time,
    ILogger<UserService> logger) : IUserService
{
    private const string DeactivatedReason = "User deactivated";

    public async Task<BaseResponse<UserDto>> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not { } userId)
        {
            return BaseResponse<UserDto>.Unauthorized(ResponseMessages.AuthenticationRequired);
        }

        var user = await users.GetByIdAsync(userId, cancellationToken);
        return user is null
            ? BaseResponse<UserDto>.NotFound(ResponseMessages.UserNotFound)
            : BaseResponse<UserDto>.Ok(user.ToDto());
    }

    public Task<BaseResponse<UserDto>> UpdateCurrentProfileAsync(UpdateUserProfileDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return BusinessErrorHandling.RunAsync(
            async () =>
            {
                if (currentUser.UserId is not { } userId)
                {
                    return BaseResponse<UserDto>.Unauthorized(ResponseMessages.AuthenticationRequired);
                }

                var user = await users.GetByIdAsync(userId, cancellationToken);
                if (user is null)
                {
                    return BaseResponse<UserDto>.NotFound(ResponseMessages.UserNotFound);
                }

                user.UpdateProfile(dto.FirstName, dto.LastName);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                logger.LogInformation("User {UserId} updated their profile", userId);
                return BaseResponse<UserDto>.Ok(user.ToDto());
            },
            (status, message) => BaseResponse<UserDto>.Fail(status, message),
            logger);
    }

    public async Task<BaseResponse<PagedResult<UserDto>>> GetPagedAsync(UserQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        if (!access.IsAdmin)
        {
            return Denied<PagedResult<UserDto>>(ResponseMessages.AdminOnly);
        }

        var page = await users.GetPagedAsync(parameters, cancellationToken);
        return BaseResponse<PagedResult<UserDto>>.Ok(page.Map(u => u.ToDto()));
    }

    public async Task<BaseResponse<IReadOnlyList<UserSummaryDto>>> LookupAsync(string? search, CancellationToken cancellationToken = default)
    {
        if (!access.HasManagementRole)
        {
            return Denied<IReadOnlyList<UserSummaryDto>>(ResponseMessages.ManagerOnly);
        }

        var term = search?.Trim();
        if (term?.Length > PagingConstraints.SearchTermMaxLength)
        {
            term = term[..PagingConstraints.SearchTermMaxLength];
        }

        var page = await users.GetPagedAsync(
            new UserQueryParameters
            {
                Search = term,
                IsActive = true,
                PageSize = PagingConstraints.LookupMaxResults,
                SortBy = UserQueryParameters.SortFields.Name,
                SortDirection = SortDirection.Asc,
            },
            cancellationToken);

        IReadOnlyList<UserSummaryDto> results = page.Items.Select(u => u.ToSummaryDto()).ToList();
        return BaseResponse<IReadOnlyList<UserSummaryDto>>.Ok(results);
    }

    public async Task<BaseResponse<UserDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!access.IsAdmin)
        {
            return Denied<UserDto>(ResponseMessages.AdminOnly);
        }

        var user = await users.GetByIdAsync(id, cancellationToken);
        return user is null
            ? BaseResponse<UserDto>.NotFound(ResponseMessages.UserNotFound)
            : BaseResponse<UserDto>.Ok(user.ToDto());
    }

    public Task<BaseResponse<UserDto>> CreateAsync(CreateUserDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return BusinessErrorHandling.RunAsync(
            async () =>
            {
                if (!access.IsAdmin)
                {
                    return Denied<UserDto>(ResponseMessages.AdminOnly);
                }

                if (await users.EmailExistsAsync(dto.Email, cancellationToken))
                {
                    return BaseResponse<UserDto>.Conflict(ResponseMessages.EmailTaken);
                }

                var user = UserAccountFactory.Create(dto.FirstName, dto.LastName, dto.Email, dto.Password, dto.Role, passwordHasher, time.GetUtcNow());
                // The admin chose this password, so it is temporary: the user must replace it at first sign-in.
                user.RequirePasswordChange();
                await users.AddAsync(user, cancellationToken);

                try
                {
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }
                catch (ConflictException)
                {
                    // A concurrent request with the same email won; the unique index caught it.
                    return BaseResponse<UserDto>.Conflict(ResponseMessages.EmailTaken);
                }

                logger.LogInformation("User {UserId} created with role {Role} by {ActorId}", user.Id, user.Role, currentUser.UserId);
                return BaseResponse<UserDto>.Created(user.ToDto());
            },
            (status, message) => BaseResponse<UserDto>.Fail(status, message),
            logger);
    }

    public Task<BaseResponse<UserDto>> ChangeRoleAsync(Guid id, ChangeUserRoleDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return BusinessErrorHandling.RunAsync(
            async () =>
            {
                if (!access.IsAdmin)
                {
                    return Denied<UserDto>(ResponseMessages.AdminOnly);
                }

                // An admin demoting themselves could leave the system with no admin at all.
                if (id == currentUser.UserId && dto.Role != UserRole.Admin)
                {
                    return BaseResponse<UserDto>.BadRequest(ResponseMessages.CannotDemoteSelf);
                }

                var user = await users.GetByIdAsync(id, cancellationToken);
                if (user is null)
                {
                    return BaseResponse<UserDto>.NotFound(ResponseMessages.UserNotFound);
                }

                user.ChangeRole(dto.Role);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                logger.LogInformation("User {UserId} role changed to {Role} by {ActorId}", id, dto.Role, currentUser.UserId);
                return BaseResponse<UserDto>.Ok(user.ToDto());
            },
            (status, message) => BaseResponse<UserDto>.Fail(status, message),
            logger);
    }

    public Task<BaseResponse> ActivateAsync(Guid id, CancellationToken cancellationToken = default) =>
        ChangeActivationAsync(id, activate: true, cancellationToken);

    public Task<BaseResponse> DeactivateAsync(Guid id, CancellationToken cancellationToken = default) =>
        ChangeActivationAsync(id, activate: false, cancellationToken);

    private Task<BaseResponse> ChangeActivationAsync(Guid id, bool activate, CancellationToken cancellationToken) =>
        BusinessErrorHandling.RunAsync(
            async () =>
            {
                if (!access.IsAdmin)
                {
                    return DeniedNoContent(ResponseMessages.AdminOnly);
                }

                if (!activate && id == currentUser.UserId)
                {
                    return BaseResponse.BadRequest(ResponseMessages.CannotDeactivateSelf);
                }

                // Deactivation must revoke the active refresh tokens, so load them with the user.
                var user = activate
                    ? await users.GetByIdAsync(id, cancellationToken)
                    : await users.GetWithActiveRefreshTokensAsync(id, cancellationToken);

                if (user is null)
                {
                    return BaseResponse.NotFound(ResponseMessages.UserNotFound);
                }

                ApplyActivation(user, activate);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                logger.LogInformation("User {UserId} {Change} by {ActorId}", id, activate ? "activated" : "deactivated", currentUser.UserId);
                return BaseResponse.NoContent();
            },
            (status, message) => BaseResponse.Fail(status, message),
            logger);

    private void ApplyActivation(User user, bool activate)
    {
        if (activate)
        {
            user.Activate();
            return;
        }

        user.Deactivate();
        user.RevokeAllRefreshTokens(time.GetUtcNow(), DeactivatedReason);
    }

    private BaseResponse<T> Denied<T>(string message)
    {
        logger.LogWarning("User {UserId} denied: {Reason}", currentUser.UserId, message);
        return BaseResponse<T>.Forbidden(message);
    }

    private BaseResponse DeniedNoContent(string message)
    {
        logger.LogWarning("User {UserId} denied: {Reason}", currentUser.UserId, message);
        return BaseResponse.Forbidden(message);
    }
}
