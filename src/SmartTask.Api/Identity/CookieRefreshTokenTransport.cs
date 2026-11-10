using SmartTask.Application.Contracts.Identity;

namespace SmartTask.Api.Identity;

/// <summary>
/// Carries the refresh token in a cookie that is:
/// <list type="bullet">
/// <item><b>HttpOnly</b>: page scripts can't read it, so XSS can't steal it.</item>
/// <item><b>Secure</b>: sent over HTTPS only.</item>
/// <item><b>SameSite=Strict</b>: not sent on cross-site requests, which blocks CSRF against refresh and logout.</item>
/// <item>Scoped to <c>/api/v1/auth</c>: not sent with any other API call.</item>
/// </list>
/// </summary>
public sealed class CookieRefreshTokenTransport(IHttpContextAccessor httpContextAccessor) : IRefreshTokenTransport
{
    public const string CookieName = "stms_refresh";
    public const string CookiePath = "/api/v1/auth";

    private HttpContext Context =>
        httpContextAccessor.HttpContext ?? throw new InvalidOperationException("The refresh token transport needs an active HTTP request.");

    public string? Read() =>
        Context.Request.Cookies.TryGetValue(CookieName, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    public void Write(string rawToken, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrEmpty(rawToken);
        Context.Response.Cookies.Append(CookieName, rawToken, CreateOptions(expiresAt));
    }

    /// <summary>Deletes the cookie. The path must match the one it was written with, or the browser keeps it.</summary>
    public void Clear() => Context.Response.Cookies.Delete(CookieName, CreateOptions(expiresAt: null));

    private static CookieOptions CreateOptions(DateTimeOffset? expiresAt) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = CookiePath,
        Expires = expiresAt,
        IsEssential = true,
    };
}
