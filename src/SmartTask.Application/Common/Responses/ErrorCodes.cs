namespace SmartTask.Application.Common.Responses;

/// <summary>
/// Stable, machine-readable codes in <see cref="IBaseResponse.ErrorCode"/>. Clients switch on these,
/// never on <see cref="IBaseResponse.Message"/>. Never rename a value after it has shipped: add a new one instead.
/// </summary>
public static class ErrorCodes
{
    /// <summary>401: no access token was sent.</summary>
    public const string AuthenticationRequired = "AUTH_REQUIRED";

    /// <summary>401: the access token has expired. The client should call <c>POST auth/refresh</c> and retry.</summary>
    public const string TokenExpired = "TOKEN_EXPIRED";

    /// <summary>401: the token's signature, issuer, audience or format is wrong. The client should sign in again.</summary>
    public const string TokenInvalid = "TOKEN_INVALID";

    /// <summary>403: the caller's role may not call this endpoint.</summary>
    public const string InsufficientRole = "INSUFFICIENT_ROLE";

    /// <summary>
    /// 403: an admin set this user's password, so it must be changed first. Only <c>GET users/me</c>,
    /// <c>POST users/me/password</c> and the auth endpoints work until then. The client should show the
    /// change-password screen.
    /// </summary>
    public const string PasswordChangeRequired = "PASSWORD_CHANGE_REQUIRED";

    /// <summary>403: the role is allowed, but not for this particular project or task.</summary>
    public const string ResourceForbidden = "RESOURCE_FORBIDDEN";

    /// <summary>Reserved for a future active-user check (the account was deactivated).</summary>
    public const string AccountInactive = "ACCOUNT_INACTIVE";
}
