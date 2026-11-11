# API Conventions — Response Contract, Base Controller, Endpoint Catalogue

Source of truth for `/implement-auth`, `/create-features`, `/create-api`, `/implement-ai`,
`/create-angular-feature` and `/write-docs`. Change the contract **here first**.

---

## 1. Request flow

```
HTTP request
  → Middleware: exception handler, HTTPS/HSTS, security headers, Serilog request logging,
                CORS, rate limiter, authentication, authorization
  → ValidationFilter (FluentValidation; short-circuits with 400 BaseResponse)
  → Controller action (one line: call service, map result)
  → I<Feature>Service (Application) — permission checks, domain calls, repositories, UoW
  → returns BaseResponse / BaseResponse<T>
  → BaseApiController.ToActionResult(...) → ObjectResult with matching status code
```

Controllers contain **no** business logic, no try/catch, no repository access and no mapping.

---

## 2. Response contract (`SmartTask.Application/Common/Responses/`)

Services return these. They depend only on `System.Net.HttpStatusCode` — no ASP.NET Core types —
so the Application layer stays framework-free.

```csharp
public interface IBaseResponse
{
    HttpStatusCode Status { get; }
    bool Success { get; }
    string? Message { get; }
    IReadOnlyDictionary<string, string[]>? Errors { get; }
    string? ErrorCode { get; }          // stable code from ErrorCodes, null when not needed
    string? TraceId { get; set; }
}
```

`BaseResponse` (no payload — delete, logout, membership changes) and `BaseResponse<T> : BaseResponse`
(adds `T? Data`).

| Member         | Notes                                                                 |
|----------------|-----------------------------------------------------------------------|
| `Status`       | `[JsonIgnore]` — the HTTP status already carries it                   |
| `StatusCode`   | `int` computed from `Status`, serialised for clients that want it in the body |
| `Success`      | computed: `2xx`                                                        |
| `Message`      | short human-readable text; never stack traces or internal details     |
| `Errors`       | field → messages (camelCase keys) for validation failures; otherwise null |
| `ErrorCode`    | machine-readable code from `ErrorCodes` (§5) for 401/403; serialised right after `message`; omitted when null |
| `TraceId`      | set by `BaseApiController` / exception handler from `HttpContext.TraceIdentifier` |
| `Data`         | payload (generic only); null on failure                               |

Static factories (both classes; the generic versions return `BaseResponse<T>`):
`Ok(data)`, `Created(data)`, `NoContent()`, `BadRequest(message)`,
`ValidationFailed(errors)`, `Unauthorized(message, errorCode? = null)`,
`Forbidden(message, errorCode = RESOURCE_FORBIDDEN)`, `NotFound(message)`,
`Conflict(message)`, `Error(message)`, `Fail(HttpStatusCode, message, errors?, errorCode?)`.
`Unauthorized` has no default code on purpose: services also return 401 for a wrong password or an
expired refresh session, which are not "sign in first".
Constructors are `protected`/`private` — callers always use factories.

Serialised shape (camelCase, nulls omitted):

```json
{ "statusCode": 404, "success": false, "message": "Project not found.", "traceId": "0HN..." }
{ "statusCode": 200, "success": true, "data": { "id": "…", "name": "Website relaunch" }, "traceId": "0HN..." }
{ "statusCode": 400, "success": false, "message": "Validation failed.",
  "errors": { "name": ["Name is required."] }, "traceId": "0HN..." }
```

Paged endpoints return `BaseResponse<PagedResult<TSummaryDto>>`.

### When services use which status

| Situation                                                       | Status |
|-----------------------------------------------------------------|--------|
| Read / update succeeded                                         | 200    |
| Created                                                         | 201    |
| Deleted / logout / member removed                               | 204    |
| `DomainException` caught (broken invariant, bad transition)     | 400    |
| Bad credentials, invalid/expired/reused refresh token           | 401    |
| Resource visible to the user but action not allowed             | 403    |
| Resource missing **or not visible to the user** (avoid enumeration) | 404 |
| Duplicate (email, project name) or concurrency conflict         | 409    |
| AI provider failed / returned unusable output                   | 502    |
| AI provider not configured / timed out                          | 503    |
| Unexpected exception — never returned by services; the global handler produces it | 500 |

Services **return** responses for expected outcomes. They do **not** throw for "not found",
"forbidden" or "conflict". Only truly unexpected failures escape as exceptions.

---

## 3. Base controller (`SmartTask.Api/Controllers/BaseApiController.cs`)

```csharp
[ApiController]
[Produces("application/json")]
public abstract class BaseApiController : ControllerBase
{
    protected ActionResult<TResponse> ToActionResult<TResponse>(TResponse response)
        where TResponse : IBaseResponse
    {
        response.TraceId ??= HttpContext.TraceIdentifier;

        return response.Status switch
        {
            HttpStatusCode.OK                  => Ok(response),
            HttpStatusCode.Created             => StatusCode((int)HttpStatusCode.Created, response),
            HttpStatusCode.NoContent           => NoContent(),               // 204 must not carry a body
            HttpStatusCode.BadRequest          => BadRequest(response),
            HttpStatusCode.Unauthorized        => Unauthorized(response),
            HttpStatusCode.Forbidden           => StatusCode((int)HttpStatusCode.Forbidden, response),
            HttpStatusCode.NotFound            => NotFound(response),
            HttpStatusCode.Conflict            => Conflict(response),
            HttpStatusCode.InternalServerError => StatusCode((int)HttpStatusCode.InternalServerError, response),
            HttpStatusCode.BadGateway          => StatusCode((int)HttpStatusCode.BadGateway, response),
            HttpStatusCode.ServiceUnavailable  => StatusCode((int)HttpStatusCode.ServiceUnavailable, response),
            _                                  => StatusCode((int)response.Status, response) // never remap an unknown code
        };
    }

    /// <summary>201 with a Location header pointing at the new resource; other statuses fall back to ToActionResult.</summary>
    protected ActionResult<BaseResponse<T>> ToCreatedResult<T>(
        BaseResponse<T> response, string actionName, Func<T, object> routeValues)
    {
        if (response.Status != HttpStatusCode.Created || response.Data is null)
            return ToActionResult(response);

        response.TraceId ??= HttpContext.TraceIdentifier;
        return CreatedAtAction(actionName, routeValues(response.Data), response);
    }
}
```

Why it deviates from a plain switch:
- **204** uses `NoContent()` with no body. Writing a body on a 204 response is invalid HTTP, and Kestrel throws.
- **409**, **502** and **503** are explicit because services return them.
- **Unknown codes pass through** unchanged instead of becoming 502, so a new status can't be
  silently turned into a misleading gateway error.

### Action shape (every controller follows this)

```csharp
[Route("api/v1/projects")]
[Authorize(Roles = AppRoles.Any)]
public sealed class ProjectsController(IProjectService projectService) : BaseApiController
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType<BaseResponse<ProjectDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BaseResponse<ProjectDto>>> GetById(Guid id, CancellationToken cancellationToken)
        => ToActionResult(await projectService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = AppRoles.Management)]           // narrows the class-level [Authorize(Roles = AppRoles.Any)]
    public async Task<ActionResult<BaseResponse<ProjectDto>>> Create(CreateProjectDto dto, CancellationToken cancellationToken)
        => ToCreatedResult(await projectService.CreateAsync(dto, cancellationToken), nameof(GetById), p => new { id = p.Id });
}
```

---

## 4. Consistent error responses outside controllers

Every non-controller error path also returns the `BaseResponse` shape:

| Source                              | How                                                            |
|-------------------------------------|----------------------------------------------------------------|
| FluentValidation                    | `ValidationFilter` (global `IAsyncActionFilter`) → 400         |
| Model binding / malformed JSON      | `ApiBehaviorOptions.InvalidModelStateResponseFactory` → 400    |
| Unauthenticated                     | `JwtBearerEvents.OnChallenge` → 401 `AUTH_REQUIRED` / `TOKEN_EXPIRED` / `TOKEN_INVALID` |
| Authenticated but wrong/unknown role | `ApiAuthorizationResultHandler` (`IAuthorizationMiddlewareResultHandler`) → 403 `INSUFFICIENT_ROLE` |
| Token has `pwd_change=required` (admin-set password) and the endpoint isn't `[AllowedWhilePasswordChangeRequired]` | same handler, after a successful role check → 403 `PASSWORD_CHANGE_REQUIRED`. Allowed: `GET users/me`, `POST users/me/password`, anonymous endpoints |
| `ForbiddenAccessException` / `UnauthenticatedException` | `GlobalExceptionHandler` → 403 (its code) / 401 `AUTH_REQUIRED`, logged at Warning |
| Rate limited                        | `RateLimiterOptions.OnRejected` → 429 + `Retry-After`          |
| Unhandled exception                 | `GlobalExceptionHandler : IExceptionHandler` → 500 (logged with trace id, generic message) |
| `DomainException` that escaped      | same handler → 400                                             |

### ValidationFilter (`SmartTask.Api/Filters/ValidationFilter.cs`)
For each action argument, resolve `IValidator<TArg>` from DI. If one exists, validate it. If it fails,
short-circuit with `BaseResponse.ValidationFailed(errors)` (camelCase keys) and status 400. Register it
globally in `AddControllers(o => o.Filters.Add<ValidationFilter>())`. Arguments without a validator are
ignored.

---

## 5. Authorization

Two layers, kept separate:

| Layer | Question | Where | Failure |
|---|---|---|---|
| **Role** | May this *kind* of user call the endpoint? | `[Authorize(Roles = ...)]` + default/fallback policy | 403 `INSUFFICIENT_ROLE` |
| **Resource** | May *this* user touch *this* project/task (owner, member, assignee)? | `IProjectAccessGuard` in the services (`/create-features`) | 404 if not visible, 403 `RESOURCE_FORBIDDEN` |

- **Role names** live in `SmartTask.Api/Authorization/AppRoles.cs`, built from `UserRole` with `nameof`:
  `Admin`, `ProjectManager`, `TeamMember`, `Management` (Admin, ProjectManager), `Any` (all three) and
  the `All` list. The token's `role` claim is the enum **name**; matching is ordinal and case-sensitive.
  A unit test keeps `AppRoles` in sync with `UserRole`.
- **Placement:** every controller except `AuthController` has a class-level baseline
  `[Authorize(Roles = AppRoles.Any)]`. Actions add `[Authorize(Roles = AppRoles.Admin | AppRoles.Management)]`
  only to narrow it (all `[Authorize]` attributes must pass, so an action can't widen the baseline).
  No named policies for roles.
- **Default and fallback policy:** authenticated **and** one of the known roles. A token with no role or
  an unknown role is refused everywhere. Anonymous endpoints (Auth, health) opt out with `[AllowAnonymous]`.
- **Failures:** `ApiAuthorizationResultHandler` writes every role failure as a `BaseResponse`; the JWT
  `OnChallenge` event writes every 401. No custom middleware: the policy system already applies the
  shared checks globally.
- **Not done (follow-up):** a per-request "user still active / role unchanged" check. The stale window is
  bounded by the 15-minute access token, refresh-token revocation on deactivation, and the guard's role checks.

### Error codes (`SmartTask.Application/Common/Responses/ErrorCodes.cs`)

```json
{ "statusCode": 403, "success": false, "message": "You do not have permission to perform this action.",
  "errorCode": "INSUFFICIENT_ROLE", "traceId": "0HN..." }
```

| Status | `errorCode` | Meaning | What the client does |
|---|---|---|---|
| 401 | `TOKEN_EXPIRED` | access token expired | call `POST auth/refresh` once, retry; sign out if the refresh fails |
| 401 | `TOKEN_INVALID` | bad signature, issuer, audience or format | clear the session, go to `/login?returnUrl=…` |
| 401 | `AUTH_REQUIRED` | no token sent | clear the session, go to `/login?returnUrl=…` |
| 403 | `INSUFFICIENT_ROLE` | the role may not call this endpoint | show "not allowed" / route to `/forbidden`; don't retry; hide such UI by role |
| 403 | `RESOURCE_FORBIDDEN` | role fine, but not for this project/task | show the message inline (toast); stay on the page |
| 403 | `PASSWORD_CHANGE_REQUIRED` | an admin set the password; it must be changed first | route to the change-password page (also when login returns `user.mustChangePassword: true`); replace the stored access token with the one the change returns |
| 401/403 | `ACCOUNT_INACTIVE` | reserved for the active-user follow-up | sign out with an "account deactivated" notice |

Codes are never renamed once shipped. Other statuses (400/404/409/5xx) carry no code yet.

---

## 6. Rate limiting policies

| Policy   | Partition        | Limit (configurable in `RateLimiting` section) | Applied to |
|----------|------------------|------------------------------------------------|------------|
| `global` | user id, else IP | 100 requests / minute, fixed window            | all endpoints (global limiter) |
| `auth`   | IP               | 5 requests / minute, sliding window             | register, login, refresh |
| `ai`     | user id          | 10 requests / minute, token bucket              | AI endpoint |

Health endpoints are excluded (`DisableRateLimiting()`).

---

## 7. Endpoint catalogue

Base path `api/v1`. Roles: **A** = Admin, **PM** = ProjectManager, **TM** = TeamMember, **anon** = anonymous.

### Auth — `AuthController` (`/implement-auth`)
| Method | Route                 | Body                 | Success | Roles | Rate limit |
|--------|-----------------------|----------------------|---------|-------|------------|
| POST   | `auth/register`       | `RegisterRequestDto` | 201 `UserDto` | anon | auth |
| POST   | `auth/login`          | `LoginRequestDto`    | 200 `AuthResponseDto` + refresh cookie | anon | auth |
| POST   | `auth/refresh`        | — (cookie)           | 200 `AuthResponseDto` + rotated cookie | anon | auth |
| POST   | `auth/logout`         | — (cookie)           | 204, cookie cleared | anon | — |

### Users — `UsersController`
| Method | Route                      | Body                   | Success | Roles |
|--------|----------------------------|------------------------|---------|-------|
| GET    | `users/me`                 | —                      | 200 `UserDto` | any |
| PUT    | `users/me`                 | `UpdateUserProfileDto` | 200 `UserDto` | any |
| POST   | `users/me/password`        | `ChangePasswordDto`    | 200 `AuthResponseDto` + new refresh cookie; other sessions signed out (400 wrong current password) | any; rate limit `auth`; allowed while a password change is required |
| GET    | `users`                    | query `UserQueryParameters` | 200 `PagedResult<UserDto>` | A |
| POST   | `users`                    | `CreateUserDto`        | 201 `UserDto` (409 if the email is taken) | A |
| GET    | `users/lookup?search=`     | —                      | 200 `IReadOnlyList<UserSummaryDto>` (max 20) | A, PM |
| GET    | `users/{id}`               | —                      | 200 `UserDto` | A |
| PATCH  | `users/{id}/role`          | `ChangeUserRoleDto`    | 200 `UserDto` | A |
| POST   | `users/{id}/activate`      | —                      | 204 | A |
| POST   | `users/{id}/deactivate`    | —                      | 204 (also revokes refresh tokens) | A |

### Projects — `ProjectsController`
| Method | Route                                  | Body                    | Success | Roles |
|--------|----------------------------------------|-------------------------|---------|-------|
| GET    | `projects`                             | query `ProjectQueryParameters` | 200 `PagedResult<ProjectSummaryDto>` | any (scoped) |
| GET    | `projects/{id}`                        | —                       | 200 `ProjectDto` | any (owner/member/admin) |
| POST   | `projects`                             | `CreateProjectDto`      | 201 `ProjectDto` | A, PM |
| PUT    | `projects/{id}`                        | `UpdateProjectDto`      | 200 `ProjectDto` | A, PM-owner |
| DELETE | `projects/{id}`                        | —                       | 204 | A, PM-owner |
| POST   | `projects/{id}/members`                | `AddProjectMemberDto`   | 204 | A, PM-owner |
| DELETE | `projects/{id}/members/{userId}`       | —                       | 204 | A, PM-owner |

### Tasks — `TasksController`
| Method | Route                                  | Body                    | Success | Roles |
|--------|----------------------------------------|-------------------------|---------|-------|
| GET    | `projects/{projectId}/tasks`           | query `TaskQueryParameters` | 200 `PagedResult<TaskSummaryDto>` | owner/member/admin |
| POST   | `projects/{projectId}/tasks`           | `CreateTaskDto`         | 201 `TaskDto` | A, PM-owner |
| GET    | `tasks`                                | query `TaskQueryParameters` | 200 `PagedResult<TaskSummaryDto>` | any (scoped) |
| GET    | `tasks/{id}`                           | —                       | 200 `TaskDto` | owner/member/admin |
| PUT    | `tasks/{id}`                           | `UpdateTaskDto`         | 200 `TaskDto` | A, PM-owner |
| DELETE | `tasks/{id}`                           | —                       | 204 | A, PM-owner |
| PATCH  | `tasks/{id}/status`                    | `UpdateTaskStatusDto`   | 200 `TaskDto` | A, PM-owner, TM-assignee |
| PATCH  | `tasks/{id}/assignee`                  | `AssignTaskDto`         | 200 `TaskDto` | A, PM-owner |

### Dashboard — `DashboardController`
| Method | Route                       | Success | Roles |
|--------|-----------------------------|---------|-------|
| GET    | `dashboard?upcomingDays=7`  | 200 `DashboardStatsDto` | any (scoped) |

### AI — `AiController` (`/implement-ai`)
| Method | Route                               | Body                           | Success | Roles | Rate limit |
|--------|-------------------------------------|--------------------------------|---------|-------|------------|
| POST   | `ai/task-descriptions/improve`      | `ImproveDescriptionRequestDto` | 200 `ImproveDescriptionResponseDto` | any | ai |

### Health (anonymous, not versioned)
| Route           | Checks |
|-----------------|--------|
| `/health/live`  | process is up (no dependencies) |
| `/health/ready` | SQL Server reachable (`AddDbContextCheck`) |
