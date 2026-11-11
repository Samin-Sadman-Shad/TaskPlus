---
description: Implement JWT authentication with rotating refresh tokens — register, login, refresh-token and logout APIs
argument-hint: "(no arguments)"
allowed-tools: Read, Write, Edit, Glob, Grep, Bash(dotnet new:*), Bash(dotnet sln:*), Bash(dotnet add:*), Bash(dotnet build:*), Bash(dotnet test:*), Bash(dotnet user-secrets:*), Bash(ls:*), Bash(mkdir:*)
---

# /implement-auth

Implement authentication end to end with **four APIs**: **register**, **login**, **refresh token**
and **logout**. Use short-lived JWT access tokens, rotating refresh tokens with reuse detection, and
password hashing.

## 0. Read context first

1. `CLAUDE.md`
2. `docs/API-CONVENTIONS.md` — response contract, base controller, auth endpoint rows, `auth` rate-limit policy
3. `docs/DOMAIN-MODEL.md` — `User`, `RefreshToken`, auth DTOs
4. Existing code: `User`/`RefreshToken` entities, `IUserRepository`, `IRefreshTokenRepository`,
   `IUnitOfWork`, `DTOs/Auth`, `Validators/Auth`

Prerequisites: `/create-domain`, `/create-repositories`, `/create-dto` and `/implement-validators`
for `Auth` must have produced their files. If something is missing, list it and stop.

## 1. Design (follow exactly)

| Concern             | Decision |
|---------------------|----------|
| Access token        | JWT, HMAC-SHA256, **15 min** lifetime (configurable), claims: `sub`, `email`, `name`, `role`, `jti`, `iat` |
| Refresh token       | 64 random bytes (`RandomNumberGenerator`), Base64Url; **7 days** lifetime (configurable) |
| Refresh storage     | Only the **SHA-256 hash** is stored in `RefreshToken.TokenHash` |
| Refresh transport   | **HttpOnly, Secure, SameSite=Strict cookie** named `stms_refresh`, `Path=/api/v1/auth`. It is never in the JSON body, so JavaScript can't read it and XSS can't steal it |
| Rotation            | Each refresh revokes the presented token (`ReplacedByTokenHash` = new hash) and issues a new one |
| Reuse detection     | If a **revoked** token is presented, revoke **all** of the user's active tokens and return 401 — a stolen token chain is cut off |
| Password hashing    | `Microsoft.AspNetCore.Identity.PasswordHasher<User>` (PBKDF2), from package `Microsoft.Extensions.Identity.Core`. Rehash on login when `VerifyHashedPassword` returns `SuccessRehashNeeded` |
| Login errors        | Always the same message, "Invalid email or password.", for unknown email, wrong password and inactive user (inactive → 401 with the same message). When the user is not found, still run a dummy hash verification so response timing doesn't reveal whether the email exists |
| Registration        | Role is always `TeamMember`. Duplicate email → 409 "An account with this email already exists." |
| Logout              | Revokes the cookie's refresh token (if any), clears the cookie and returns 204 — **idempotent**, works even when the access token has expired |
| Housekeeping        | Revoked/expired tokens older than 30 days can be deleted later through `IRefreshTokenRepository.RemoveExpiredAsync` (a hosted service is optional, not required here) |

The access token can't be revoked before it expires. That's accepted because its lifetime is short.
Document this in the summary.

## 2. Application layer

### Contracts — `Application/Contracts/Identity/`
```csharp
public interface IPasswordHasher            // wraps PasswordHasher<User>
{
    string Hash(User user, string password);
    PasswordCheckResult Verify(User user, string hash, string password); // own enum: Failed, Success, SuccessRehashNeeded (avoids clashing with Identity's enum)
}

public interface IJwtTokenService
{
    AccessTokenResult CreateAccessToken(User user);              // Token, ExpiresAt
}

public interface IRefreshTokenService
{
    GeneratedRefreshToken Generate();                            // RawToken, TokenHash, ExpiresAt
    string Hash(string rawToken);
}

/// Port so the Application layer can set/read the refresh token without knowing about HTTP cookies.
public interface IRefreshTokenTransport
{
    string? Read();
    void Write(string rawToken, DateTimeOffset expiresAt);
    void Clear();
}

public interface ICurrentUserService                             // create if missing
{
    Guid? UserId { get; }
    UserRole? Role { get; }
    string? IpAddress { get; }
    bool IsAuthenticated { get; }
}
```

### Service — `Application/Contracts/Services/IAuthService.cs` + `Application/Services/Auth/AuthService.cs`
```csharp
public interface IAuthService
{
    Task<BaseResponse<UserDto>>         RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);
    Task<BaseResponse<AuthResponseDto>> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
    Task<BaseResponse<AuthResponseDto>> RefreshAsync(CancellationToken cancellationToken = default);
    Task<BaseResponse>                  LogoutAsync(CancellationToken cancellationToken = default);
    // POST users/me/password (UsersController): verify the current password (400 on currentPassword),
    // reject a new password containing the email local part (400 on newPassword), then
    // user.ChangePasswordHash (revokes every refresh token, clears MustChangePassword) and issue a fresh
    // session for this device in the same SaveChanges → 200 AuthResponseDto + new cookie.
    Task<BaseResponse<AuthResponseDto>> ChangePasswordAsync(ChangePasswordDto request, CancellationToken cancellationToken = default);
}
```

`AuthService` takes in its constructor: `IUserRepository`, `IRefreshTokenRepository`, `IUnitOfWork`,
`IPasswordHasher`, `IJwtTokenService`, `IRefreshTokenService`, `IRefreshTokenTransport`,
`ICurrentUserService` (for the IP address), `TimeProvider`, `ILogger<AuthService>`.

Flows:
- **Register:** email exists → 409. Otherwise `User.Create(...)`, hash the password, add, save → 201 `UserDto`.
  Catch the unique-index violation on save (the race condition case) → 409.
- **Login:** look up by email → verify → reject inactive users → `RecordLogin(now)` → rehash if needed →
  issue tokens (helper `IssueTokensAsync(user)`: generate refresh token, `user.AddRefreshToken(...)`,
  save, `transport.Write(...)`, return `AuthResponseDto`) → 200.
- **Refresh:** read the raw token from the transport. Missing → 401. Hash it and look it up.
  Not found → 401 and clear the cookie. Revoked → **reuse detected**: revoke all of the user's tokens,
  save, log a warning, clear the cookie, 401. Expired or inactive user → 401. Otherwise rotate → 200.
- **Logout:** read the token. If found and active, revoke it with reason "Logout" and save.
  Always clear the cookie → 204.

Logging: log `UserId` and the event (`Login succeeded`, `Refresh token reuse detected`). **Never** log
passwords, raw tokens, hashes or full JWTs.

### Models — `Application/Common/Models/Auth/`
`AccessTokenResult`, `GeneratedRefreshToken`. Update `AuthResponseDto` so it holds `AccessToken`,
`AccessTokenExpiresAt` and `User`, and **no refresh token** (it travels in the cookie). Delete
(Older runs may have created) `RefreshTokenRequestDto` and `LogoutRequestDto` and their validators if they exist, and update
`docs/DOMAIN-MODEL.md`'s non-entity DTO table to match.

Register in `AddApplication()`: `services.AddScoped<IAuthService, AuthService>();`

## 3. Infrastructure layer — `SmartTask.Infrastructure`

Create the project if missing (`classlib`, net10.0, references Application, added to the solution).

```
SmartTask.Infrastructure/
├── Identity/
│   ├── JwtOptions.cs            Issuer, Audience, SigningKey, AccessTokenMinutes = 15, RefreshTokenDays = 7
│   ├── JwtTokenService.cs       uses JsonWebTokenHandler (Microsoft.IdentityModel.JsonWebTokens)
│   ├── RefreshTokenService.cs
│   └── PasswordHasherAdapter.cs
└── DependencyInjection.cs       AddInfrastructure(IConfiguration)
```

- Bind `JwtOptions` from section `Jwt` with `.ValidateDataAnnotations()` (or a custom
  `IValidateOptions<JwtOptions>`) and `.ValidateOnStart()`. The signing key must be **≥ 32 bytes**, so
  a missing or weak key fails the app on startup instead of on the first login.
- `RefreshTokenService.Hash` → `SHA256.HashData` → hex string.
- Register `TimeProvider.System` with `TryAddSingleton`.

## 4. Api layer — `SmartTask.Api`

Create the Web API project if missing (`dotnet new webapi --use-controllers -n SmartTask.Api -o src/SmartTask.Api -f net10.0`),
reference Application, Persistence and Infrastructure, add it to the solution, and delete the
WeatherForecast sample.

**Shared API foundations:** if `BaseResponse` (Application), `BaseApiController`, `ValidationFilter`
or `Policies` don't exist yet, create them **exactly** as specified in `docs/API-CONVENTIONS.md` §2–§5.
`/create-api` will reuse them, so don't create alternative versions.

- `Identity/CookieRefreshTokenTransport.cs` implements `IRefreshTokenTransport` using
  `IHttpContextAccessor`. Cookie: `HttpOnly = true`, `Secure = true`, `SameSite = Strict`,
  `Path = "/api/v1/auth"`, `Expires = expiresAt`, `IsEssential = true`. Delete the cookie with the same path.
- `Identity/CurrentUserService.cs` implements `ICurrentUserService` from claims and
  `Connection.RemoteIpAddress`.
- **Authentication setup** (`Extensions/AuthenticationExtensions.cs` → `AddJwtAuthentication(config)`):
  `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)` with
  `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey` all true,
  `ClockSkew = TimeSpan.FromSeconds(30)`, `RoleClaimType = ClaimTypes.Role`, `MapInboundClaims = false`
  (set `NameClaimType`/`RoleClaimType` accordingly). `OnChallenge`/`OnForbidden` events write a
  `BaseResponse` 401/403 body (API-CONVENTIONS §4).
- **Authorization:** fallback policy = authenticated user; `AdminOnly` and `ProjectManagement` policies.
- **Rate limiting:** register the `auth` policy (and the `global` limiter if missing) per
  API-CONVENTIONS §6, with `OnRejected` writing a 429 `BaseResponse`.

### `Controllers/AuthController.cs`
```csharp
[Route("api/v1/auth")]
[AllowAnonymous]
public sealed class AuthController(IAuthService authService) : BaseApiController
{
    [HttpPost("register")] [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<ActionResult<BaseResponse<UserDto>>> Register(RegisterRequestDto request, CancellationToken cancellationToken)
        => ToActionResult(await authService.RegisterAsync(request, cancellationToken));

    [HttpPost("login")]    [EnableRateLimiting(RateLimitPolicies.Auth)]  ...LoginAsync
    [HttpPost("refresh")]  [EnableRateLimiting(RateLimitPolicies.Auth)]  ...RefreshAsync
    [HttpPost("logout")]                                                  ...LogoutAsync
}
```
Each action is a single line plus `[ProducesResponseType]` attributes for each documented status code.

### Configuration
- `appsettings.json` gets a `Jwt` section with `Issuer`, `Audience`, `AccessTokenMinutes` and
  `RefreshTokenDays` — **no `SigningKey`**.
- Run `dotnet user-secrets init -p src/SmartTask.Api` and tell the user to set
  `dotnet user-secrets set "Jwt:SigningKey" "<64+ random chars>" -p src/SmartTask.Api` (don't put
  a real key in any file).
- `Program.cs` order: `UseExceptionHandler` → `UseHttpsRedirection` → `UseCors` → `UseRateLimiter` →
  `UseAuthentication` → `UseAuthorization` → `MapControllers`. Keep `Program.cs` short by calling
  extension methods.

## 5. Tests — `tests/SmartTask.Application.UnitTests/Services/Auth/`

Use NSubstitute for the contracts and `FakeTimeProvider` for time. Cover:
- register: success, duplicate email → 409, role is TeamMember
- login: success sets the cookie (`transport.Received().Write(...)`), wrong password → 401,
  unknown email → 401 with the **same message**, inactive → 401, rehash-needed path updates the hash
- refresh: missing cookie → 401, rotation revokes the old token and writes a new one,
  **reuse of a revoked token revokes all tokens**, expired → 401
- logout: revokes and clears; no cookie → still 204

Also add `tests/SmartTask.Infrastructure.UnitTests` (create if missing) with a `JwtTokenService` test
that decodes the token and asserts its claims and expiry.

## 6. Verify

```bash
dotnet build SmartTask.sln
dotnet test SmartTask.sln
grep -rniE "SigningKey\"\s*:\s*\"[^\"]+" src/   # must print nothing — no keys in config files
```

Self-check, report ✅/❌:
- [ ] Exactly four auth endpoints, each a one-line action on `BaseApiController` (the password change lives on `UsersController`, which isn't anonymous)
- [ ] Password change needs the current password, signs out other sessions and keeps this one; the JWT carries `pwd_change=required` only while `MustChangePassword` is true
- [ ] Refresh token only ever travels in an HttpOnly/Secure/SameSite=Strict cookie and is stored hashed
- [ ] Rotation and reuse detection implemented and tested
- [ ] Same error message for unknown email, wrong password and inactive user
- [ ] Self-registration can't choose a role
- [ ] JWT options validated on startup, key not in source
- [ ] `auth` rate-limit policy applied to register/login/refresh
- [ ] Nothing sensitive logged

## 7. Finish

1. Update `docs/PROGRESS.md`.
2. Print the endpoints with sample request/response bodies and the user-secrets command to run.
3. Suggested commit: `feat(auth): JWT auth with rotating refresh tokens`.
4. Don't implement project, task, user or dashboard endpoints here.
