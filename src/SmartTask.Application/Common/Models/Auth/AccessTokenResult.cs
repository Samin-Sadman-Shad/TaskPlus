namespace SmartTask.Application.Common.Models.Auth;

/// <summary>A signed access token (JWT) and when it expires.</summary>
public sealed record AccessTokenResult(string Token, DateTimeOffset ExpiresAt)
{
    /// <summary>Hides the token so the record can be logged safely.</summary>
    public override string ToString() => $"{nameof(AccessTokenResult)} {{ Token = ***, ExpiresAt = {ExpiresAt:O} }}";
}
