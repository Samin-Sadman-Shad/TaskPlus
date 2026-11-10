using Microsoft.Extensions.Logging;
using SmartTask.Application.Common.Exceptions;
using SmartTask.Application.Common.Models.Auth;
using SmartTask.Application.Common.Responses;
using SmartTask.Application.Contracts.Identity;
using SmartTask.Application.Contracts.Persistence;
using SmartTask.Application.Contracts.Services;
using SmartTask.Application.DTOs.Auth;
using SmartTask.Application.DTOs.Users;
using SmartTask.Domain.Entities;

namespace SmartTask.Application.Services.Auth;

/// <summary>
/// Registration, login and session handling. Access tokens are short-lived JWTs. Refresh tokens are
/// random, stored only as hashes, rotated on every use, and travel only in an HttpOnly cookie. If a
/// revoked refresh token is presented again, it was probably stolen, so all of that user's sessions
/// are revoked.
/// </summary>
/// <remarks>
/// Logging records user ids and events only. It never records passwords, raw tokens, hashes or JWTs.
/// </remarks>
public sealed class AuthService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokens,
    IRefreshTokenService refreshTokenService,
    IRefreshTokenTransport transport,
    ICurrentUserService currentUser,
    TimeProvider time,
    ILogger<AuthService> logger) : IAuthService
{
    private const string RotatedReason = "Rotated";
    private const string ReuseDetectedReason = "Reuse detected";
    private const string LogoutReason = "Logout";

    /// <summary>
    /// <see cref="User.Create"/> needs a hash, and hashing needs the user instance. This placeholder is
    /// replaced in the same operation, before anything is saved.
    /// </summary>
    private const string PendingPasswordHash = "pending";

    /// <summary>Used only to spend the same hashing time for an unknown email as for a known one.</summary>
    private static readonly User TimingDummyUser = User.Create("Timing", "Dummy", "timing.dummy@invalid.local", PendingPasswordHash);
    private const string TimingDummyPassword = "timing-dummy-password";
    private static string? s_timingDummyHash;

    public Task<BaseResponse<UserDto>> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return BusinessErrorHandling.RunAsync(
            async () =>
            {
                if (await users.EmailExistsAsync(request.Email, cancellationToken))
                {
                    return BaseResponse<UserDto>.Conflict(ResponseMessages.EmailTaken);
                }

                // User.Create always produces a TeamMember: the request has no role to choose.
                var user = User.Create(request.FirstName, request.LastName, request.Email, PendingPasswordHash);
                user.ChangePasswordHash(passwordHasher.Hash(user, request.Password), time.GetUtcNow());
                await users.AddAsync(user, cancellationToken);

                try
                {
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }
                catch (ConflictException)
                {
                    // Another registration with the same email won the race; the unique index caught it.
                    return BaseResponse<UserDto>.Conflict(ResponseMessages.EmailTaken);
                }

                logger.LogInformation("User {UserId} registered", user.Id);
                return BaseResponse<UserDto>.Created(user.ToDto());
            },
            (status, message) => BaseResponse<UserDto>.Fail(status, message),
            logger);
    }

    public Task<BaseResponse<AuthResponseDto>> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return BusinessErrorHandling.RunAsync(
            async () =>
            {
                var user = await users.GetByEmailAsync(request.Email, cancellationToken);
                if (user is null)
                {
                    SpendEquivalentHashingTime(request.Password);
                    return BaseResponse<AuthResponseDto>.Unauthorized(ResponseMessages.InvalidCredentials);
                }

                var check = passwordHasher.Verify(user, user.PasswordHash, request.Password);
                if (check is not (PasswordCheckResult.Success or PasswordCheckResult.SuccessRehashNeeded) || !user.IsActive)
                {
                    logger.LogWarning("Login failed for user {UserId}", user.Id);
                    return BaseResponse<AuthResponseDto>.Unauthorized(ResponseMessages.InvalidCredentials);
                }

                if (check == PasswordCheckResult.SuccessRehashNeeded)
                {
                    user.RehashPassword(passwordHasher.Hash(user, request.Password));
                }

                user.RecordLogin(time.GetUtcNow());
                var response = await IssueTokensAsync(user, refreshTokenService.Generate(), cancellationToken);

                logger.LogInformation("Login succeeded for user {UserId}", user.Id);
                return BaseResponse<AuthResponseDto>.Ok(response);
            },
            (status, message) => BaseResponse<AuthResponseDto>.Fail(status, message),
            logger);
    }

    public Task<BaseResponse<AuthResponseDto>> RefreshAsync(CancellationToken cancellationToken = default) =>
        BusinessErrorHandling.RunAsync(
            async () =>
            {
                var presented = await FindPresentedTokenAsync(cancellationToken);
                if (presented is null)
                {
                    return SessionExpired();
                }

                var now = time.GetUtcNow();

                if (presented.IsRevoked)
                {
                    await RevokeAllSessionsAsync(presented.UserId, now, cancellationToken);
                    logger.LogWarning("Refresh token reuse detected for user {UserId}; all sessions revoked", presented.UserId);
                    return SessionExpired();
                }

                if (presented.IsExpired(now))
                {
                    return SessionExpired();
                }

                var user = await users.GetWithActiveRefreshTokensAsync(presented.UserId, cancellationToken);
                if (user is not { IsActive: true })
                {
                    return SessionExpired();
                }

                // Rotation: the presented token is revoked and points to its successor, so a later
                // reuse of it is detected above.
                var next = refreshTokenService.Generate();
                presented.Revoke(now, RotatedReason, next.TokenHash);
                var response = await IssueTokensAsync(user, next, cancellationToken);

                logger.LogInformation("Refresh token rotated for user {UserId}", user.Id);
                return BaseResponse<AuthResponseDto>.Ok(response);
            },
            (status, message) => BaseResponse<AuthResponseDto>.Fail(status, message),
            logger);

    public Task<BaseResponse> LogoutAsync(CancellationToken cancellationToken = default) =>
        BusinessErrorHandling.RunAsync(
            async () =>
            {
                var presented = await FindPresentedTokenAsync(cancellationToken);
                var now = time.GetUtcNow();

                if (presented is not null && presented.IsActive(now))
                {
                    presented.Revoke(now, LogoutReason);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    logger.LogInformation("User {UserId} logged out", presented.UserId);
                }

                transport.Clear();
                return BaseResponse.NoContent();
            },
            (status, message) => BaseResponse.Fail(status, message),
            logger);

    /// <summary>
    /// Stores the hash of <paramref name="refresh"/> on the user, saves (one save for the whole
    /// operation), then hands the raw token to the transport and returns a new access token.
    /// </summary>
    private async Task<AuthResponseDto> IssueTokensAsync(User user, GeneratedRefreshToken refresh, CancellationToken cancellationToken)
    {
        var token = RefreshToken.Create(user.Id, refresh.TokenHash, refresh.ExpiresAt, time.GetUtcNow(), currentUser.IpAddress);
        user.AddRefreshToken(token);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Write the cookie only after the token is persisted, so the client never holds a token the server doesn't know.
        transport.Write(refresh.RawToken, refresh.ExpiresAt);
        var access = jwtTokens.CreateAccessToken(user);

        return new AuthResponseDto
        {
            AccessToken = access.Token,
            AccessTokenExpiresAt = access.ExpiresAt,
            User = user.ToDto(),
        };
    }

    /// <summary>The stored token matching the transport's raw token, or <c>null</c> if there is none or it is unknown.</summary>
    private async Task<RefreshToken?> FindPresentedTokenAsync(CancellationToken cancellationToken)
    {
        var raw = transport.Read();
        return string.IsNullOrWhiteSpace(raw)
            ? null
            : await refreshTokens.GetByTokenHashAsync(refreshTokenService.Hash(raw), cancellationToken);
    }

    private async Task RevokeAllSessionsAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var user = await users.GetWithActiveRefreshTokensAsync(userId, cancellationToken);
        if (user is null)
        {
            return;
        }

        user.RevokeAllRefreshTokens(now, ReuseDetectedReason);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private BaseResponse<AuthResponseDto> SessionExpired()
    {
        transport.Clear();
        return BaseResponse<AuthResponseDto>.Unauthorized(ResponseMessages.SessionExpired);
    }

    /// <summary>
    /// Verifies the password against a real hash of a dummy user, so a request for an unknown email
    /// takes about as long as one for a known email. Otherwise the response time would reveal which
    /// emails are registered.
    /// </summary>
    private void SpendEquivalentHashingTime(string password)
    {
        var dummyHash = s_timingDummyHash ??= passwordHasher.Hash(TimingDummyUser, TimingDummyPassword);
        _ = passwordHasher.Verify(TimingDummyUser, dummyHash, password);
    }
}
