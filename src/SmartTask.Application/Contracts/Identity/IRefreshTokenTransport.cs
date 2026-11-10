namespace SmartTask.Application.Contracts.Identity;

/// <summary>
/// Moves the raw refresh token between server and client without the Application layer knowing how.
/// The API implements it with an HttpOnly, Secure, SameSite=Strict cookie, so page scripts can never
/// read the token (strategy pattern: the transport can change without touching <c>AuthService</c>).
/// </summary>
public interface IRefreshTokenTransport
{
    /// <summary>The raw token the client presented, or <c>null</c> if there is none.</summary>
    string? Read();

    void Write(string rawToken, DateTimeOffset expiresAt);

    void Clear();
}
