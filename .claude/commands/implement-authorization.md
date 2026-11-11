---
description: Implement role-based authorization on every API endpoint ([Authorize(Roles = ...)]), the global authorization hooks, and machine-readable 401/403 error codes for the client
argument-hint: "[Users | Projects | Tasks | Dashboard | Ai]  (optional — omit for every controller + the global setup)"
allowed-tools: Read, Write, Edit, Glob, Grep, Bash(dotnet build:*), Bash(dotnet test:*), Bash(ls:*), Bash(mkdir:*), Bash(grep:*)
---

# /implement-authorization

Secure every endpoint with **role-based authorization** taken from `CLAUDE.md` §4. Give every
authentication and authorization failure a **stable error code** that the Angular client can act on.

Authorization has two layers. Keep them separate:

| Layer | Question it answers | Where | Failure |
|---|---|---|---|
| **Role** (this command) | "May this *kind* of user call this endpoint at all?" | `[Authorize(Roles = ...)]` on controllers and actions, plus the global fallback policy | 403 `INSUFFICIENT_ROLE` |
| **Resource** (already exists) | "May *this* user touch *this* project or task?" (owner, member, assignee) | `IProjectAccessGuard` in the services (`/create-features` §3) | 404 if not visible, 403 `RESOURCE_FORBIDDEN` |

Role checks don't need data, so they run in the pipeline before the controller.
Resource checks need data, so they **stay in the services**. Never move them into attributes or filters.

**Scope:** `$ARGUMENTS` is one controller name, or empty. The global setup in §3 and §4 is applied
the first time this command runs, whatever the scope.

## 0. Read context first

1. `CLAUDE.md`: §4 Roles & permissions (the source of truth for who may do what), §6 rules
2. `docs/API-CONVENTIONS.md`:
   - §1 request flow (authentication → authorization run before the controller)
   - §2 response contract
   - §4 error responses outside controllers
   - §5 Authorization
   - §7 endpoint catalogue (the Roles column)
3. `.claude/commands/create-features.md` §3: `IProjectAccessGuard`, and the 404-vs-403 rule
4. `.claude/commands/create-api.md`: controller rules, `[ProducesResponseType]` requirements, pipeline order
5. `docs/ARCHITECTURE.md`: which layer each new type belongs to
6. Existing code:
   - `src/SmartTask.Api/Authorization/` (`Policies.cs`)
   - `src/SmartTask.Api/Controllers/`
   - `src/SmartTask.Api/Extensions/AuthenticationExtensions.cs` (`AddJwtAuthentication`, `AddApiAuthorization`, `JwtBearerEvents`)
   - `src/SmartTask.Api/Http/ApiResponseWriter.cs`
   - `src/SmartTask.Api/Middleware/GlobalExceptionHandler.cs`
   - `src/SmartTask.Application/Common/Responses/` (`BaseResponse`, `IBaseResponse`, `ResponseMessages`)
   - `src/SmartTask.Application/Common/Exceptions/`
   - `src/SmartTask.Application/Services/Authorization/ProjectAccessGuard.cs`
   - `src/SmartTask.Infrastructure/Identity/AppClaimTypes.cs` (role claim `"role"`; `MapInboundClaims = false`)

**Prerequisites:** `/implement-auth`, `/create-features` and `/create-api` have run, so the
controllers, the JWT setup and `BaseResponse` exist. If a controller in scope is missing, skip it and
report it. If `CLAUDE.md` §4 and API-CONVENTIONS §7 disagree on an endpoint's roles, **stop and ask**.

## 1. Role constants: `src/SmartTask.Api/Authorization/AppRoles.cs`

The source of truth is `SmartTask.Domain.Enums.UserRole` (`Admin = 1`, `ProjectManager = 2`,
`TeamMember = 3`). Roles reach the API like this:
- `JwtTokenService` writes the role claim `"role"` as `user.Role.ToString()`, which is the enum
  **name**, never the number.
- `AddJwtAuthentication` sets `RoleClaimType = AppClaimTypes.Role`, so `IsInRole` and
  `[Authorize(Roles = ...)]` read that claim.
- Role matching is ordinal and case-sensitive. The constants must therefore be the exact enum names.

`[Authorize(Roles = ...)]` needs compile-time strings, so build them from the enum with `nameof`.
A renamed member then breaks the build instead of silently letting nobody in. Don't hard-code
`"Admin"` anywhere else in the API.

```csharp
using SmartTask.Domain.Enums;

namespace SmartTask.Api.Authorization;

/// <summary>
/// Role names for <c>[Authorize(Roles = ...)]</c>, taken from <see cref="UserRole"/> so they can't drift.
/// A comma-separated value means "any of these roles". Several <c>[Authorize]</c> attributes on the
/// same endpoint must <b>all</b> pass.
/// </summary>
public static class AppRoles
{
    public const string Admin          = nameof(UserRole.Admin);
    public const string ProjectManager = nameof(UserRole.ProjectManager);
    public const string TeamMember     = nameof(UserRole.TeamMember);

    /// <summary>Admin or ProjectManager: create projects, and manage projects and tasks.</summary>
    public const string Management = Admin + "," + ProjectManager;

    /// <summary>Every known role: the baseline for every authenticated endpoint.</summary>
    public const string Any = Admin + "," + ProjectManager + "," + TeamMember;

    /// <summary>The same list as an array, for <c>RequireRole(...)</c> in policy builders.</summary>
    public static readonly string[] All = [Admin, ProjectManager, TeamMember];
}
```

`nameof` protects against **renames**, but not against **additions**. If someone adds a fourth
`UserRole` member, `Any` and `All` won't include it, and users with the new role are refused
everywhere. A C# `const` can't be computed from `Enum.GetNames`, so add a unit test that keeps them
in sync (§7):

```csharp
AppRoles.All.ShouldBe(Enum.GetNames<UserRole>(), ignoreOrder: true);
AppRoles.Any.Split(',').ShouldBe(Enum.GetNames<UserRole>(), ignoreOrder: true);
AppRoles.Management.Split(',').ShouldBe([nameof(UserRole.Admin), nameof(UserRole.ProjectManager)], ignoreOrder: true);
```

When a role is added, update `AppRoles`, the §2 matrix and `CLAUDE.md` §4 together.

Then remove the named role policies:
- Delete `AdminOnly` and `ProjectManagement` from `AddApiAuthorization()`.
- Delete `Authorization/Policies.cs`. First run `grep -rn "Policies\." src tests` (ignoring `RateLimitPolicies`).
  If anything else still uses `Policies`, migrate it first.

## 2. Endpoint → roles matrix, and where each attribute goes

### Placement rules
1. Every `[Authorize]` attribute on an endpoint (controller **and** action) must pass. They are ANDed.
   So an action-level attribute can **narrow** the controller's rule but never **widen** it.
2. Every controller except `AuthController` gets a class-level **baseline** `[Authorize(Roles = AppRoles.Any)]`.
3. If **every** action in a controller has the same rule, the attribute lives **only on the
   controller**. Otherwise, add an action-level attribute only on the actions that need more than the baseline.
4. Don't repeat the baseline on actions. Don't use `[Authorize(Policy = ...)]` for roles.
5. Ownership, membership and assignee rules **never** go in attributes. The service enforces them
   through `IProjectAccessGuard` (PM-owner, TM-assignee).
6. `AuthController` keeps its class-level `[AllowAnonymous]`. The `logout` action is anonymous on purpose:
   it is cookie-based.

### Matrix (from CLAUDE.md §4 and API-CONVENTIONS §7)

| Controller | Class level | Action | Route | Action-level attribute | Service check |
|---|---|---|---|---|---|
| **AuthController** | `[AllowAnonymous]` | Register, Login, Refresh, Logout | `auth/*` | — | — |
| **UsersController** | `Roles = AppRoles.Any` | GetCurrent / UpdateCurrentProfile | `GET/PUT users/me` | — | — |
| | | ChangePassword | `POST users/me/password` | — (`[EnableRateLimiting(auth)]`) | current password required |
| | | GetPaged | `GET users` | `Roles = AppRoles.Admin` | — |
| | | Create | `POST users` | `Roles = AppRoles.Admin` | email unique (409) |
| | | Lookup | `GET users/lookup` | `Roles = AppRoles.Management` | — |
| | | GetById | `GET users/{id}` | `Roles = AppRoles.Admin` | — |
| | | ChangeRole | `PATCH users/{id}/role` | `Roles = AppRoles.Admin` | no self-demotion |
| | | Activate / Deactivate | `POST users/{id}/activate`, `.../deactivate` | `Roles = AppRoles.Admin` | no self-deactivation |
| **ProjectsController** | `Roles = AppRoles.Any` | GetPaged / GetById | `GET projects`, `GET projects/{id}` | — | scoped list; visible or 404 |
| | | Create | `POST projects` | `Roles = AppRoles.Management` | — |
| | | Update / Delete | `PUT/DELETE projects/{id}` | `Roles = AppRoles.Management` | Admin or PM-owner, else 403 |
| | | AddMember / RemoveMember | `POST projects/{id}/members`, `DELETE .../members/{userId}` | `Roles = AppRoles.Management` | Admin or PM-owner |
| **TasksController** | `Roles = AppRoles.Any` | GetPagedByProject / GetPaged / GetById | `GET projects/{projectId}/tasks`, `GET tasks`, `GET tasks/{id}` | — | scoped; visible or 404 |
| | | Create | `POST projects/{projectId}/tasks` | `Roles = AppRoles.Management` | Admin or PM-owner |
| | | Update / Delete / Assign | `PUT/DELETE tasks/{id}`, `PATCH tasks/{id}/assignee` | `Roles = AppRoles.Management` | Admin or PM-owner |
| | | UpdateStatus | `PATCH tasks/{id}/status` | — (all roles) | Admin, PM-owner or TM-assignee |
| **DashboardController** | `Roles = AppRoles.Any` | Get | `GET dashboard` | — | scope by role |
| **AiController** | `Roles = AppRoles.Any` | ImproveDescription | `POST ai/task-descriptions/improve` | — | — |

Dashboard and AI have a single shared rule, so the attribute lives **only at controller level**.

Example of the resulting shape:

```csharp
[Route("api/v1/users")]
[Authorize(Roles = AppRoles.Any)]
[ProducesResponseType<BaseResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<BaseResponse>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<BaseResponse>(StatusCodes.Status429TooManyRequests)]
public sealed class UsersController(IUserService userService) : BaseApiController
{
    [HttpGet("me")]                                   // baseline only
    public async Task<ActionResult<BaseResponse<UserDto>>> GetCurrent(CancellationToken cancellationToken)
        => ToActionResult(await userService.GetCurrentAsync(cancellationToken));

    [HttpGet]
    [Authorize(Roles = AppRoles.Admin)]               // narrows the baseline
    public async Task<ActionResult<BaseResponse<PagedResult<UserDto>>>> GetPaged([FromQuery] UserQueryParameters parameters, CancellationToken cancellationToken)
        => ToActionResult(await userService.GetPagedAsync(parameters, cancellationToken));
}
```

Also update each controller's XML `<summary>`. Replace mentions of "the `ProjectManagement` policy"
with the roles.

## 3. Global authorization: what is shared by all endpoints

Assessment, so the reasoning is recorded:
- **Every** endpoint shares two checks: "authenticated" and "has a known role". The policy system
  already handles both globally, so a custom request middleware would only duplicate
  `UseAuthorization()`. **Don't write one.**
- **Every** authorization failure must return the same `BaseResponse` shape with an error code. The
  framework's hook for this is `IAuthorizationMiddlewareResultHandler`, which is global and runs once
  per failed request. Use it instead of a middleware.
- **Not added:** a database check on every request for "is the user still active / is the role still
  current". It costs a database round-trip per request. The stale window is already bounded: access
  tokens last 15 minutes (`Jwt:AccessTokenMinutes`), `DeactivateAsync` revokes every refresh token,
  and `ProjectAccessGuard` re-checks the role on writes. Add it to `docs/PROGRESS.md` as an optional
  follow-up (a security-stamp claim checked against a short-lived cache), not as part of this command.

### 3.1 Policies in `AddApiAuthorization()` (`Extensions/AuthenticationExtensions.cs`)

```csharp
public static IServiceCollection AddApiAuthorization(this IServiceCollection services)
{
    // Authenticated AND one of the known roles. A token with no role or an unknown role is refused.
    var knownRoleUser = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireRole(AppRoles.All)
        .Build();

    services.AddAuthorizationBuilder()
        .SetDefaultPolicy(knownRoleUser)    // a bare [Authorize]
        .SetFallbackPolicy(knownRoleUser);  // endpoints with no attribute at all (safety net)

    services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiAuthorizationResultHandler>();
    return services;
}
```

`[AllowAnonymous]` (Auth controller, health checks) still opts out. Don't change the pipeline order
in `UseApiPipeline()`. `UseAuthentication → UseAuthorization` must stay before `UseRateLimiter`.

### 3.2 `src/SmartTask.Api/Authorization/ApiAuthorizationResultHandler.cs`

```csharp
using System.Security.Claims;                 // FindFirstValue
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;   // RolesAuthorizationRequirement
using Microsoft.AspNetCore.Authorization.Policy;           // AuthorizationMiddlewareResultHandler, PolicyAuthorizationResult
using SmartTask.Api.Http;
using SmartTask.Application.Common.Responses;
using SmartTask.Infrastructure.Identity;      // AppClaimTypes

namespace SmartTask.Api.Authorization;

/// <summary>
/// The single place where a failed authorization becomes an HTTP response. Forbidden results
/// (authenticated, wrong role) get a 403 <see cref="BaseResponse"/> with <see cref="ErrorCodes.InsufficientRole"/>.
/// Everything else (success, and challenge for unauthenticated callers) goes to the default handler,
/// so challenges still reach <c>JwtBearerEvents.OnChallenge</c>.
/// </summary>
public sealed class ApiAuthorizationResultHandler(ILogger<ApiAuthorizationResultHandler> logger)
    : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden)
        {
            var requiredRoles = authorizeResult.AuthorizationFailure?.FailedRequirements
                .OfType<RolesAuthorizationRequirement>()
                .SelectMany(r => r.AllowedRoles);

            logger.LogWarning("Forbidden {Method} {Path} for user {UserId} with role {Role}; required one of {RequiredRoles} (trace {TraceId})",
                context.Request.Method, context.Request.Path,
                context.User.FindFirstValue(AppClaimTypes.Subject), context.User.FindFirstValue(AppClaimTypes.Role),
                requiredRoles, context.TraceIdentifier);

            await ApiResponseWriter.WriteAsync(context,
                BaseResponse.Forbidden(ResponseMessages.InsufficientRole, ErrorCodes.InsufficientRole),
                context.RequestAborted);
            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
```

- Log ids and role names only. Never log the token or the email.
- The client gets the generic message. It is **not** told which roles would have been allowed.
- Remove `OnForbidden` from `JwtBearerEvents` in `AddJwtAuthentication`: this handler replaces it.
  Keep `OnChallenge` (§4.3).

## 4. Error contract for the client

### 4.1 `src/SmartTask.Application/Common/Responses/ErrorCodes.cs`

These live in Application because services return them too.

```csharp
/// <summary>
/// Stable, machine-readable codes in <c>BaseResponse.ErrorCode</c>. Clients switch on these, never on
/// <c>Message</c>. Never rename a value after it has shipped: add a new one instead.
/// </summary>
public static class ErrorCodes
{
    public const string AuthenticationRequired = "AUTH_REQUIRED";      // 401: no token sent
    public const string TokenExpired           = "TOKEN_EXPIRED";      // 401: the client should call /auth/refresh
    public const string TokenInvalid           = "TOKEN_INVALID";      // 401: bad signature, issuer or format → sign in again
    public const string InsufficientRole       = "INSUFFICIENT_ROLE";  // 403: the role may not call this endpoint
    public const string PasswordChangeRequired = "PASSWORD_CHANGE_REQUIRED"; // 403: admin-set password must be changed first
    public const string ResourceForbidden      = "RESOURCE_FORBIDDEN"; // 403: the role is fine, but not for this project or task
    public const string AccountInactive        = "ACCOUNT_INACTIVE";   // 401/403: reserved for the active-user follow-up
}
```

Add `InsufficientRole = "You do not have permission to perform this action."` to `ResponseMessages`.

### 4.2 `ErrorCode` on the response

- Add `string? ErrorCode { get; }` to `IBaseResponse`, and implement it in `BaseResponse`.
  - Serialize it as `errorCode`. The global `WhenWritingNull` already omits it when null.
  - Give it `[JsonPropertyOrder]` so it comes right after `message`.
- Extend the factories with an **optional** code. Existing call sites keep compiling:
  ```csharp
  public static BaseResponse Unauthorized(string message, string? errorCode = null) => ...;   // no default: login/refresh failures are 401 too
  public static BaseResponse Forbidden(string message, string errorCode = ErrorCodes.ResourceForbidden) => ...;
  public static BaseResponse Fail(HttpStatusCode status, string message, IReadOnlyDictionary<string, string[]>? errors = null, string? errorCode = null) => ...;
  ```
  The default for `Forbidden` is `RESOURCE_FORBIDDEN`. Every existing `Forbidden(...)` call in the
  services comes from `IProjectAccessGuard`, so the default is correct for those calls.
  `BaseResponseOfT.cs` hides these with its own `new` factories (`Unauthorized`, `Forbidden`, `Fail`).
  Give them the same optional parameters, and add the matching `protected` constructor overload.
- Don't add codes to other statuses (400/404/409/5xx) in this command. They are out of scope.

### 4.3 `OnChallenge` codes (`AddJwtAuthentication`)

Change `WriteUnauthorizedAsync` so the client can tell "refresh" from "sign in":

| Situation | Code | Message |
|---|---|---|
| `context.AuthenticateFailure is SecurityTokenExpiredException` | `TOKEN_EXPIRED` | "The access token has expired." |
| any other `AuthenticateFailure` (signature, issuer, malformed) | `TOKEN_INVALID` | "The access token is invalid." (add it to `ResponseMessages`) |
| no failure: no token was sent | `AUTH_REQUIRED` | `ResponseMessages.AuthenticationRequired` |

Keep `context.HandleResponse()` and the `WWW-Authenticate: Bearer` header.

### 4.4 Exceptions: `src/SmartTask.Application/Common/Exceptions/`

Use them for code paths that **cannot** return a `BaseResponse` (helpers deep in a call chain,
future background work). Services keep **returning** `BaseResponse.Forbidden(...)`. Don't throw for
control flow.

```csharp
/// <summary>The caller is authenticated but may not perform the operation. Mapped to 403.</summary>
public sealed class ForbiddenAccessException(string message, string errorCode = ErrorCodes.ResourceForbidden)
    : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
}

/// <summary>No usable identity for an operation that needs one. Mapped to 401.
/// (Named to avoid clashing with <see cref="System.UnauthorizedAccessException"/>.)</summary>
public sealed class UnauthenticatedException(string message = ResponseMessages.AuthenticationRequired)
    : Exception(message);
```

Map them in `GlobalExceptionHandler`'s switch. Put them before the default case, and log them at
**Warning**, not Error:
```csharp
ForbiddenAccessException forbidden => BaseResponse.Forbidden(forbidden.Message, forbidden.ErrorCode),
UnauthenticatedException unauthenticated => BaseResponse.Unauthorized(unauthenticated.Message),
```
Update the handler's XML summary and the table in API-CONVENTIONS §4.

### 4.5 Client contract

Document this in API-CONVENTIONS §5. `/create-angular-feature core` implements it in the HTTP
error interceptor.

```json
{ "statusCode": 403, "success": false, "message": "You do not have permission to perform this action.",
  "errorCode": "INSUFFICIENT_ROLE", "traceId": "0HN..." }
```

| Status | `errorCode` | What the client does |
|---|---|---|
| 401 | `TOKEN_EXPIRED` | Call `POST auth/refresh` once, then retry the original request. If the refresh fails, sign out |
| 401 | `TOKEN_INVALID`, `AUTH_REQUIRED` | Clear the session and go to `/login` with a `returnUrl` |
| 403 | `INSUFFICIENT_ROLE` | Show "not allowed" (or route to `/forbidden`). Don't retry. Hide the matching UI by role |
| 403 | `PASSWORD_CHANGE_REQUIRED` | Route to the change-password page. `ApiAuthorizationResultHandler` returns it on a **successful** role check when the token has `pwd_change=required` and the endpoint lacks `[AllowedWhilePasswordChangeRequired]` (only `GetCurrent` and `ChangePassword` have it; a metadata test pins that list) |
| 403 | `RESOURCE_FORBIDDEN` | Show the message inline (e.g. a toast). Stay on the page |
| 403/401 | `ACCOUNT_INACTIVE` | Reserved: sign out with an "account deactivated" notice |

## 5. Swagger metadata

- Every controller with the `AppRoles.Any` baseline declares
  `[ProducesResponseType<BaseResponse>(StatusCodes.Status403Forbidden)]` at class level, next to the
  existing 401 and 429. A token with an unknown role can be refused on any of its endpoints.
- Remove the now-duplicate action-level 403 declarations, **unless** the action's service also
  returns 403 (they are the same type, so either level is fine; keep one).
- Swagger's JWT security requirement is unchanged.

## 6. Documentation to update

- `docs/API-CONVENTIONS.md`:
  - §2: add `ErrorCode` to `IBaseResponse` and the member table
  - §3: update the action-shape example to `[Authorize(Roles = AppRoles.Management)]`
  - §4: 403 source is now `ApiAuthorizationResultHandler`; add the new exceptions
  - §5: rewrite it with the role layer vs the resource layer, `AppRoles`, the default/fallback policy,
    the error codes and the client contract (§4.5)
- `.claude/commands/create-api.md`: replace `[Authorize(Policy = Policies.ProjectManagement)]` and the
  `Policies.cs` mention with `AppRoles`.
- `docs/WORKFLOW.md`: add `/implement-authorization` after `/create-api` in the recommended order and in
  the dependency table (needs: controllers, the JWT setup and `BaseResponse`).

## 7. Tests

### Integration: `tests/SmartTask.Api.IntegrationTests/AuthorizationTests.cs`

Use the existing `SmartTaskApiFactory` and `[Collection(ApiCollection.Name)]`.
- Self-registration only creates TeamMembers. To get Admin or ProjectManager tokens, seed the users
  through a DI scope, then mint the token with the app's `IJwtTokenService` resolved from `factory.Services`.
- For expired or unknown-role tokens, sign one by hand with the factory's `JwtOptions` key and issuer.

Cover:
- [ ] anonymous `GET /api/v1/dashboard` → 401, `errorCode = AUTH_REQUIRED`, `WWW-Authenticate: Bearer`
- [ ] expired token → 401 `TOKEN_EXPIRED`; garbage token → 401 `TOKEN_INVALID`
- [ ] TeamMember `GET /api/v1/users` → 403 `INSUFFICIENT_ROLE`, in the `BaseResponse` shape with a `traceId`
- [ ] TeamMember `POST /api/v1/projects` → 403 `INSUFFICIENT_ROLE`
- [ ] ProjectManager `GET /api/v1/users/lookup` → 200; ProjectManager `GET /api/v1/users` → 403
- [ ] Admin `GET /api/v1/users` → 200
- [ ] a valid signature with role `"Hacker"`, the numeric role `"1"`, a lower-case `"admin"`, or no role claim on `GET /api/v1/users/me` → 403
- [ ] TeamMember `PATCH tasks/{id}/status` on a task assigned to someone else → 403 `RESOURCE_FORBIDDEN`
      (proves that the role layer let it through and the service refused it)
- [ ] `/health/live` stays anonymous → 200

### Metadata guard (no HTTP)

Use reflection over every `BaseApiController` subclass except `AuthController`.
Assert two things:
- each controller has a class-level `AuthorizeAttribute` whose `Roles` is not empty
- no action uses `AuthorizeAttribute.Policy`

This catches a future controller added without authorization.

### Unit tests

Put them in `tests/SmartTask.Application.UnitTests`, and in the API test project for the handler
and `AppRoles`.
- `AppRoles` matches `UserRole`. Use the three assertions from §1. They fail when an enum member is added
  without updating `AppRoles`.
- `BaseResponse.Forbidden("x")` serializes `"errorCode":"RESOURCE_FORBIDDEN"`. `BaseResponse.Ok()` has no `errorCode` property.
- `GlobalExceptionHandler`: `ForbiddenAccessException` → 403 with its code, `UnauthenticatedException` → 401.
- Existing service tests that assert 403 still pass without changes.

## 8. Verify

```bash
dotnet build SmartTask.slnx
dotnet test SmartTask.slnx
grep -rn "Policy = " src/SmartTask.Api/Controllers        # must print nothing
grep -rn "OnForbidden" src/SmartTask.Api                   # must print nothing
grep -rn "User.FindFirst\|User.IsInRole" src/SmartTask.Api/Controllers   # must print nothing
```

Self-check, report ✅/❌:
- [ ] Every endpoint in API-CONVENTIONS §7 within scope has the roles from the §2 matrix
- [ ] Shared rules sit at controller level; only narrowing rules sit on actions
- [ ] No resource or ownership rule in attributes; `IProjectAccessGuard` unchanged
- [ ] Default and fallback policy = authenticated + known role; `[AllowAnonymous]` only on Auth and health
- [ ] Every 401 and 403 returns the `BaseResponse` shape with an `errorCode` and a `traceId`
- [ ] No role names, tokens or emails in client-facing messages; denials logged at Warning with ids only
- [ ] Application still has no ASP.NET Core reference (`ErrorCodes` and the exceptions are plain C#)
- [ ] Docs updated (API-CONVENTIONS §2/§4/§5, `create-api.md`, `WORKFLOW.md`)
- [ ] Build passes, tests pass (report the count)

## 9. Finish

1. Update `docs/PROGRESS.md`:
   - files created and changed
   - build and test results
   - the deferred "active-user / security-stamp check" follow-up from §3
2. Print the final table: controller → action → route → effective roles → service-level check.
3. Suggested commit: `feat(api): role-based authorization and auth error codes`.
4. Stay in scope: don't change services, the domain or the Angular client in this command.
