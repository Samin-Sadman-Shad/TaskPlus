using SmartTask.Application.DTOs.Users;

namespace SmartTask.Application.DTOs.Auth;

/// <summary>Response of <c>POST auth/login</c> and <c>POST auth/refresh</c>.</summary>
/// <remarks>
/// The refresh token is <b>not</b> part of this DTO. It is sent only as an HttpOnly, Secure,
/// SameSite=Strict cookie, so page scripts can never read it. Refresh and logout read it from that
/// cookie and have no request body.
/// </remarks>
public sealed record AuthResponseDto
{
    public required string AccessToken { get; init; }

    public required DateTimeOffset AccessTokenExpiresAt { get; init; }

    public required UserDto User { get; init; }

    /// <summary>Hides the access token, so the DTO can be logged safely.</summary>
    public override string ToString() =>
        $"{nameof(AuthResponseDto)} {{ {nameof(AccessToken)} = {AuthRedaction.Mask}, " +
        $"{nameof(AccessTokenExpiresAt)} = {AccessTokenExpiresAt:O}, {nameof(User)} = {User} }}";
}
