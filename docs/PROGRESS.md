# Progress Log

Updated by each Claude Code command when it finishes. Newest entry first.

| Step | Command                 | Status      | Scope | Notes |
|------|-------------------------|-------------|-------|-------|
| 1    | `/create-domain`        | Done        | all   | Build ✅; Tests ✅ (53 passed) |
| 2    | `/create-repositories`  | Done        | all   | Build ✅; Tests ✅ (53); LocalDB smoke test ✅ (40 checks); no migration yet |
| 3    | `/create-dto`           | Done        | all   | Build ✅; Tests ✅ (53); sensitive-field grep empty; scratch mapping check ✅ (10) |
| 4    | `/implement-validators` | Done        | all   | Build ✅; Tests ✅ (232 = 53 Domain + 179 Application) |
| 5    | `/create-features`      | Done        | Users, Projects, Tasks, Dashboard | Tests verified later (2 failures found and fixed in `1b6be76`) |
| 6    | `/implement-auth`       | Done        | all   | Build ✅; Tests ✅ (353 = 55 Domain + 276 Application + 22 Infrastructure); HTTP end-to-end on LocalDB ✅ |
| 7    | `/create-api`           | Done        | all (AiController pending /implement-ai) | Build ✅; Tests ✅ (382, +29 API integration); InitialCreate migration applied to LocalDB; /swagger and /health/ready ✅ |
| 8    | `/implement-ai`         | Done        | all   | Build ✅; Tests ✅ (457 = 55 Domain + 309 Application + 58 Infrastructure + 35 API); smoke run on LocalDB with the fake provider ✅; secret grep clean |
| 9    | `/implement-authorization` | Done     | all   | Build ✅ (0 warnings); Tests ✅ (489 = 55 Domain + 314 Application + 58 Infrastructure + 62 API) |
| 10   | `/deploy-backend-services` | Done     | all   | Build ✅ (0 warnings); Tests ✅ (496 = 55 Domain + 314 Application + 58 Infrastructure + 69 API); `docker compose up` ✅ (sqlserver healthy, init exit 0, api up); InitialCreate applied by `MigrateAsync` in the container; data survives down/up ✅ |

## Log

<!-- Commands append entries below in this format:
### YYYY-MM-DD — /command-name [scope]
- Created: …
- Updated: …
- Build: ✅ / Tests: ✅ (n passed)
- Open questions / follow-ups: …
-->

### 2026-11-11 — Change password and temporary passwords for admin-created users
- Spec: `CLAUDE.md` §4 (change own password, temporary-password rule), `DOMAIN-MODEL.md` (`User.MustChangePassword`,
  `RequirePasswordChange()`, `ChangePasswordDto`), `API-CONVENTIONS.md` (`POST users/me/password`, 403 `PASSWORD_CHANGE_REQUIRED`).
- Domain: `MustChangePassword` (cleared by `ChangePasswordHash`, kept by `RehashPassword`). Migration
  `AddUserMustChangePassword` (`bit NOT NULL DEFAULT 0`; existing users unaffected), applied by `MigrateAsync` in Docker.
- Application: `ChangePasswordDto` + validator; `IAuthService.ChangePasswordAsync` (current password required, email
  rule, all other sessions revoked, fresh session for this device, one save); `UserService.CreateAsync` marks the
  password temporary; `UserDto.MustChangePassword`; `ErrorCodes.PasswordChangeRequired`.
- Infrastructure: the JWT carries `pwd_change=required` only while the flag is set.
- Api: `POST users/me/password` (any role, `auth` rate limit); `[AllowedWhilePasswordChangeRequired]` on `GetCurrent` and
  `ChangePassword`; `ApiAuthorizationResultHandler` returns 403 `PASSWORD_CHANGE_REQUIRED` everywhere else.
- Tests: +3 Domain, +15 Application (service and validator), +2 Infrastructure (claim), +4 API (full first sign-in flow,
  self-registered user never blocked, allow-list pinned, metadata).
- Verified in Docker: migration applied on start; admin-created user signs in with `mustChangePassword: true`, gets 403
  `PASSWORD_CHANGE_REQUIRED` on `/projects`, changes the password, and the new token works.
- Build: ✅ (0 warnings) / Tests: ✅ (553 = 58 Domain + 357 Application + 60 Infrastructure + 78 API)
- Follow-ups: the bootstrap admin's configured password is not marked temporary (it was chosen by the operator);
  an admin "reset password" endpoint would reuse `RequirePasswordChange()`.

### 2026-11-11 — First admin bootstrap and admin-created users
- Spec: `CLAUDE.md` §4 (admins create users with any role; first-admin bootstrap rules), `DOMAIN-MODEL.md`
  (`CreateUserDto`, `UserAccountFactory`), `API-CONVENTIONS.md` §7 (`POST users`, Admin, 201/409).
- Created: `DTOs/Users/CreateUserDto.cs`, `Validators/Users/CreateUserDtoValidator.cs`, `Services/Users/UserAccountFactory.cs`
  (one create → hash → role path for registration, admin-created users, the bootstrap admin and the dev seed),
  `Contracts/Services/IAdminBootstrapService.cs` + `Services/Users/AdminBootstrapService.cs`,
  `Api/Extensions/AdminBootstrapExtensions.cs` (`Bootstrap:Admin` settings → service, after the dev seed),
  `UsersController.Create` (`POST users`, Admin). Tests: `UserServiceCreateTests` (8), `AdminBootstrapServiceTests` (5),
  `CreateUserDtoValidatorTests` (14), 4 API tests (403 for TM/PM, Admin creates a PM who signs in, missing role → 400, duplicate → 409).
- Updated: `IUserRepository.ActiveUserWithRoleExistsAsync`; `RuleBuilderExtensions.NotContainEmailLocalPart` (moved out of the
  register validator, now shared); `AuthService.RegisterAsync` and `DevelopmentDataSeeder` use the factory; compose/.env
  (`BOOTSTRAP_ADMIN_*`, values in the git-ignored `.env` only); command files `create-dto`, `implement-validators`,
  `create-features`, `create-api`, `implement-authorization`, `write-docs`, `deploy-backend-services`.
- Verified in Docker: bootstrap admin created on first start, skipped after a restart (still exactly one admin); it signs
  in as Admin, creates a ProjectManager through `POST /users`, who signs in as ProjectManager and can create a project;
  the PM gets 403 `INSUFFICIENT_ROLE` on `POST /users`.
- Build: ✅ (0 warnings) / Tests: ✅ (529 = 55 Domain + 342 Application + 58 Infrastructure + 74 API)
- Decisions: the bootstrap never promotes an existing account with the configured email and never overwrites a password;
  only **active** admins count, so a deployment whose admins were all deactivated can recover with a new email.
- Follow-ups: no change-password endpoint exists yet, so the bootstrap/initial passwords can't be rotated through the API.

### 2026-11-11 — /deploy-backend-services
- Created: `src/SmartTask.Api/Dockerfile` (two stages: sdk:10.0 restore+publish → aspnet:10.0, non-root, `/app/logs` owned by the app user),
  `.dockerignore`, `docker-compose.yml` (sqlserver, one-shot sqlserver-init, api, commented `smart-task-web` slot, network `smarttask-network`),
  `.env.example` (placeholders; `.env` is generated locally and git-ignored), `docker/sqlserver/init.sql` (idempotent: database, `Samin` login with
  `CHECK_POLICY = OFF`, `db_owner`), `src/SmartTask.Api/appsettings.Docker.json` (connection string without password, Swagger on, migrations on),
  `Api/Extensions/DatabaseMigrationExtensions.cs` (`MigrateAsync` at start-up when `Database:ApplyMigrationsOnStartup` is true, or unset and
  `DOTNET_RUNNING_IN_CONTAINER=true`), `Api/Swagger/AuthorizeOperationFilter.cs`, `DatabaseMigrationExtensionsTests` (6), one Swagger pipeline test.
- Updated: `Persistence/DependencyInjection.cs` (`Database:Password` applied with `SqlConnectionStringBuilder`; retry count/delay from
  `Database:MaxRetryCount` / `Database:MaxRetryDelaySeconds`), `Program.cs` (one line), `SwaggerExtensions.cs` (`CustomSchemaIds` →
  `BaseResponse<UserDto>`; padlock and roles per operation instead of a global requirement), `SmartTaskApiFactory` (migrations off for SQLite),
  `.gitignore` (`*.pfx`). `SmartTaskDbContext`: no change needed (no `OnConfiguring`, no connection string).
- Ports: API 5180 (HTTP, redirects) / 7180 (HTTPS), SQL Server 1433, all from launchSettings / defaults, all free.
- Data: `D:\Projects\TaskPlus\sqlserver-data\data` and `\log` (bind volumes); SQL Server's `secrets/` is a Docker-managed volume.
- Problems found while verifying and fixed:
  - Mounting all of `/var/opt/mssql` from a Windows folder crash-loops SQL Server (`Failed to load LSA: 0xc0070102`) → split into data/log/secrets.
  - On the second start the health check (`SELECT 1`) passed while `SmartTaskDb` was still recovering, so init failed with Msg 904 →
    health check now requires every database `ONLINE`, init retries up to 10 times.
- Build: ✅ (0 warnings) / Tests: ✅ (496 passed)
- Open questions / follow-ups:
  - `Samin` / `Samin` is for local development only (policy check off). Use a strong secret for anything shared.
  - The dev HTTPS certificate is not trusted yet: run `dotnet dev-certs https --trust` once.
  - Schema ids contain `<`, `>` and `,`, outside the OpenAPI component-name pattern: `/write-docs` must check the Postman import.
  - Option still open: let `MigrateAsync` create the database too (init.sql only creates the login) or connect as `sa` and drop init.sql.

### 2026-11-11 — /implement-authorization
- Created: `Api/Authorization/AppRoles.cs` (role names from `UserRole` via `nameof`; `Management`, `Any`, `All`),
  `Api/Authorization/ApiAuthorizationResultHandler.cs` (global `IAuthorizationMiddlewareResultHandler`: 403 `INSUFFICIENT_ROLE`),
  `Application/Common/Responses/ErrorCodes.cs`, `Application/Common/Exceptions/ForbiddenAccessException.cs`,
  `Application/Common/Exceptions/UnauthenticatedException.cs`.
- Deleted: `Api/Authorization/Policies.cs` (named policies `AdminOnly` / `ProjectManagement` replaced by `[Authorize(Roles = ...)]`).
- Updated:
  - `AuthenticationExtensions`: default + fallback policy = authenticated user with a known role; handler registered;
    `OnForbidden` removed; `OnChallenge` now sets `AUTH_REQUIRED` / `TOKEN_EXPIRED` / `TOKEN_INVALID`.
  - `BaseResponse` / `BaseResponse<T>` / `IBaseResponse`: `ErrorCode` (after `message`, omitted when null);
    `Forbidden` defaults to `RESOURCE_FORBIDDEN`; `Unauthorized` and `Fail` take an optional code.
  - `ResponseMessages`: `TokenExpired`, `TokenInvalid`, `InsufficientRole`.
  - `GlobalExceptionHandler`: `ForbiddenAccessException` → 403 (its code), `UnauthenticatedException` → 401 `AUTH_REQUIRED`.
  - Controllers: class-level `[Authorize(Roles = AppRoles.Any)]` + 403 response type on Users, Projects, Tasks, Dashboard, Ai;
    action-level `Admin` / `Management` only where the matrix narrows it.
  - Docs: API-CONVENTIONS §2/§3/§4/§5, `create-api.md`, `WORKFLOW.md`; comments in `UserService`, `ChangeUserRoleDtoValidator`, `AuthFlowTests`.
  - Tests: `AuthorizationTests` (18 HTTP cases), `AuthorizationMetadataTests` (AppRoles ↔ UserRole sync, controller attributes,
    exception mapping), `BaseResponseErrorCodeTests`. `SqliteModelCustomizer` now gives row-version columns a `randomblob(8)`
    default; before, no integration test could create a project or task on SQLite.
- Build: ✅ (0 warnings, 0 errors) / Tests: ✅ (489 passed). Verify greps (`Policy = `, `OnForbidden`, `User.FindFirst` in controllers) empty.
- Deviation from the command: `Unauthorized(...)` has **no** default code (the command said `AUTH_REQUIRED`), because login
  ("Invalid email or password.") and refresh failures also return 401. Only the pipeline challenge sets a 401 code.
- Open questions / follow-ups:
  - Optional: per-request "user still active / role unchanged" check (security-stamp claim + short-lived cache), would use `ACCOUNT_INACTIVE`.
  - The services' own role checks (`UserService` AdminOnly/ManagerOnly, `ProjectService` ManagerOnly) are now unreachable over HTTP and
    would report `RESOURCE_FORBIDDEN`; pass `ErrorCodes.InsufficientRole` there if they are kept (services were out of scope).

### 2026-11-11 — /implement-ai
- Endpoint `POST api/v1/ai/task-descriptions/improve` (`AiController`, one line, `ai` rate-limit policy, which already existed from /create-api;
  input validated by the existing `ImproveDescriptionRequestDtoValidator` through `ValidationFilter`). Nothing is persisted.
- Domain: `AiConstraints` + `OutputTargetLength` (1200), `MaxSuggestions` (3), `SuggestionMaxLength` (200); DOMAIN-MODEL.md updated.
- Application:
  - `Contracts/Ai/`: `ITaskDescriptionImprover` (Strategy port), `TaskDescriptionPromptInput`, `ImprovedDescriptionResult`,
    `AiFailureReason`, `AiProviderException`; `Contracts/Services/IAiService`.
  - `Services/Ai/Prompts/TaskDescriptionPrompt` (version `v1`, system message, `<task>` user template, `<`/`>` escaping),
    `Services/Ai/AiService` (provider failures → 502/503, JSON parse + one `{…}` extraction retry, length / blank / prompt-echo checks,
    suggestions cleaned and capped), `Services/Ai/AiOutputSanitizer` (entity decode → tag strip → line endings → trim).
  - New `ResponseMessages`: `AiUnavailable`, `AiBusy`, `AiInvalidResponse`. `AiService` registered in `AddApplication()`.
- Infrastructure `Ai/`: `AiOptions` (+ `AiOptionsValidator`, ValidateOnStart), `AiProvider`, `AiProviderSelection`, `AiProviderStartupLogger`
  (hosted service; warning when GitHubModels has no key), `GitHubModelsTaskDescriptionImprover` (Microsoft.Extensions.AI `IChatClient` over the
  OpenAI SDK, keyed singleton; JSON response format; linked-CTS timeout; SDK retry policy with 2 retries; exceptions → `AiProviderException`),
  `FakeTaskDescriptionImprover` (deterministic). Packages: Microsoft.Extensions.AI.OpenAI 11.0.0 (OpenAI 2.14.0),
  Microsoft.Extensions.Hosting.Abstractions 10.0.14. `InternalsVisibleTo` for the Infrastructure unit tests.
- Api: `appsettings.json` `Ai` section (no key). Integration test host forces `Ai:Provider=Fake`.
- PROMPTS.md (repo root): all six sections. The model examples are illustrative; the fake-provider example is real output.
- Tests: +33 Application (`AiServiceTests`, `TaskDescriptionPromptTests`, including a no-text-in-logs test), +36 Infrastructure
  (provider against a stub HTTP handler through the real SDK pipeline: wire request, 429 retry, error mapping, timeout, cancellation;
  fake determinism; options validator; provider selection and the startup warning), +6 API integration (401, 200, 400, per-user 429).
- Smoke run (Development, LocalDB, JWT key from a process env var, no AI key): startup warning logged, endpoint 200 / 400, log line has only
  metadata. It left one `smoke-…@example.com` TeamMember in the local DB.
- Decisions:
  - `AiProviderException` and `AiFailureReason` are in Application/Contracts/Ai, not Infrastructure as the command sketch shows: `AiService` catches
    the exception, and Application may not reference Infrastructure.
  - `TaskDescriptionPrompt.SystemMessage` is `static readonly`, not `const`, so the limits it states come from `AiConstraints` (no magic numbers).
  - Resilience uses the OpenAI SDK's own retry pipeline (it isn't a typed `HttpClient`, so `AddStandardResilienceHandler` doesn't apply).
  - Over-long or blank suggestions are dropped rather than failing the call; an over-long description fails (502).
  - Provider 401/403/400 map to `Unavailable` (503); the HTTP status is logged for diagnosis.
- Open: real model outputs in PROMPTS.md need an `Ai:ApiKey` (/write-docs). The Angular UI should tell users that the text goes to a third-party provider.

### 2026-11-11 — /create-api (all)
- Controllers (one-line actions on `BaseApiController`, only the service interface injected, XML summaries,
  `ProducesResponseType` for every service status plus 401/429): UsersController, ProjectsController, TasksController
  (routes under `projects/{projectId}/tasks` and `tasks`), DashboardController. AuthController (from /implement-auth) already followed the rules.
  AiController is skipped: its service doesn't exist yet (`/implement-ai`).
- Host setup (reusing the /implement-auth foundations, nothing duplicated):
  - `Extensions/`: SwaggerExtensions (**Swashbuckle 11**, XML comments, JWT bearer scheme + requirement; UI in Development or when `Swagger:Enabled`),
    CorsExtensions (`Frontend` policy from `Cors:AllowedOrigins`, credentials, explicit methods/headers, exposes Location/Retry-After),
    HealthCheckExtensions (`/health/live`, `/health/ready` with the DbContext check, anonymous, no rate limiting, JSON writer without exception details),
    RateLimitingExtensions (+ `ai` token bucket per user), ServiceCollectionExtensions (`AddApiServices(config, env)`; Kestrel body limit 1 MB),
    WebApplicationExtensions (`UseApiPipeline()` in the order /create-api specifies; `UserId`/`TraceId` pushed to the Serilog LogContext; `SeedDevelopmentDataAsync()`)
  - `Middleware/SecurityHeadersMiddleware` (nosniff, DENY, no-referrer, CSP `default-src 'none'; frame-ancestors 'none'`, not on /swagger)
  - `Program.cs` (45 lines): Serilog bootstrap logger + `UseSerilog(ReadFrom.Configuration/Services, FromLogContext)`, `try/catch/finally CloseAndFlush`
    (`HostAbortedException` from `dotnet ef` is not logged as fatal)
  - `appsettings.json`: Serilog (Console + rolling compact-JSON file `logs/`), Cors, RateLimiting (global/auth/ai), Swagger off, Seed off,
    LocalDB connection with integrated security; no secrets. `appsettings.Development.json`: Swagger on, Debug logging.
  - Api csproj: `GenerateDocumentationFile` with CS1591 suppressed; packages Serilog.AspNetCore 10.0.0, Swashbuckle.AspNetCore 11.0.0,
    HealthChecks.EntityFrameworkCore 10.0.14, EF Core Design 10.0.14.
- Database: local tool manifest `dotnet-tools.json` (dotnet-ef 10.0.14; run `dotnet tool restore`). Migration
  `Persistence/Migrations/*_InitialCreate` created and applied to LocalDB `SmartTask`.
  `Persistence/Seed/DevelopmentDataSeeder`: Development **and** `Seed:Enabled` only. It migrates, then creates 3 users
  (passwords from user-secrets `Seed:AdminPassword`, `Seed:ManagerPassword`, `Seed:MemberPassword`), 2 projects and 3 tasks. Idempotent.
- Tests: `tests/SmartTask.Api.IntegrationTests` (WebApplicationFactory + in-memory SQLite, one shared host per collection). Covers health,
  401 envelope (+ WWW-Authenticate, garbage token), 400 validation and malformed JSON, security headers, CORS allowed/denied,
  Swagger document (bearer scheme, routes, XML comments), a full register → login (cookie flags) → /users/me → 403 → refresh → 204 logout flow,
  and BaseApiController mapping for every status (204 → NoContentResult, unknown status passes through, ToCreatedResult).
  Persistence gets `InternalsVisibleTo` for this project (to re-register the interceptors).
  - SQLite can't compare `DateTimeOffset` (it broke refresh in the first run). A test-only `SqliteModelCustomizer` stores them as `long`;
    the production mapping is unchanged.
- Build ✅ / Tests ✅ **382** (55 Domain, 276 Application, 22 Infrastructure, 29 API). Running against LocalDB: `/health/ready` Healthy,
  `/swagger` loads with all 21 routes, anonymous call → 401 envelope, request log lines carry TraceId/UserId.
- Decisions:
  - The global rate limit uses `GlobalLimiter` (applies to every endpoint) instead of `RequireRateLimiting("global")` on MapControllers.
    It has the same effect, and health endpoints opt out with `DisableRateLimiting()`.
  - Write endpoints that the table marks "A, PM-owner" use the `ProjectManagement` policy. Ownership is checked in the services.
    `PATCH tasks/{id}/status` has no role policy because team-member assignees may call it.
  - The Swagger UI is served by middleware before authentication, so it stays reachable while every endpoint requires a token.
- Open: AiController (/implement-ai). Running the seeder needs the three `Seed:*Password` user-secrets.

### 2026-11-11 — /implement-auth
- Created projects: `src/SmartTask.Infrastructure` (classlib) and `src/SmartTask.Api` (webapi, WeatherForecast and the
  OpenApi package removed; launch ports 7180/5180; user-secrets initialised), plus `tests/SmartTask.Infrastructure.UnitTests`.
- Domain: `User.RehashPassword(newHash)`. It upgrades the hash format on login **without** revoking sessions
  (`ChangePasswordHash` revokes them). Two tests added.
- Application:
  - `Contracts/Identity/`: IPasswordHasher + PasswordCheckResult, IJwtTokenService, IRefreshTokenService, IRefreshTokenTransport;
    `ICurrentUserService` gained `IpAddress`
  - `Common/Models/Auth/`: AccessTokenResult, GeneratedRefreshToken (both redact secrets in `ToString()`)
  - `Contracts/Services/IAuthService` + `Services/Auth/AuthService` (registered in `AddApplication()`); new auth messages in `ResponseMessages`
  - Nothing to delete: no `RefreshTokenRequestDto`/`LogoutRequestDto` existed, and DOMAIN-MODEL already describes the cookie transport.
- Infrastructure: `Identity/` JwtOptions (+ JwtOptionsValidator, ValidateOnStart, key ≥ 32 bytes), AppClaimTypes,
  JwtTokenService (JsonWebTokenHandler, HS256), RefreshTokenService (64 random bytes, Base64Url; SHA-256 lower-hex hash),
  PasswordHasherAdapter (Identity `PasswordHasher<User>`); `DependencyInjection.AddInfrastructure()`.
  Packages: IdentityModel.JsonWebTokens 8.23.0, Extensions.Identity.Core 10.0.14 (pinned; it pulled 11.0.0 by default).
- Api:
  - Shared foundations exactly as in API-CONVENTIONS: `Controllers/BaseApiController`, `Filters/ValidationFilter`,
    `Authorization/Policies`, `RateLimiting/RateLimitPolicies`
  - `Identity/CurrentUserService`, `Identity/CookieRefreshTokenTransport`
  - `Extensions/AuthenticationExtensions` (`AddJwtAuthentication`, `AddApiAuthorization`: fallback = authenticated, AdminOnly, ProjectManagement),
    `Extensions/RateLimitingExtensions` (`global` and `auth`), `Extensions/ServiceCollectionExtensions` (`AddApiServices`),
    `Extensions/WebApplicationExtensions` (`UseApiPipeline`)
  - `Middleware/GlobalExceptionHandler`, `Http/ApiResponseWriter` (BaseResponse JSON outside MVC), `Controllers/AuthController`
  - `appsettings.json`: LocalDB connection (integrated security), `Jwt` without key, `RateLimiting`
- Tests: AuthServiceTests (17), Infrastructure tests (22: JWT claims/lifetime/signature, refresh token format/hash/expiry,
  hasher round-trip/salt/malformed hash, options validator). Total **353 passing**.
- End-to-end over HTTPS against a throwaway LocalDB database (created with EnsureCreated, then dropped). All checked:
  register (201, a `role` field in the body is ignored), duplicate → 409, validation and malformed-JSON → 400 BaseResponse,
  wrong password and unknown email → the same 401, login cookie `HttpOnly; Secure; SameSite=Strict; path=/api/v1/auth`,
  rotation, reuse of an old token → 401 **and** the successor revoked, logout → token revoked, no-cookie logout → 204,
  6th auth call/min → 429 with `Retry-After`. The DB holds 64-char hashes only. The app refuses to start without a signing key.
  - Found and fixed: the sliding-window limiter gives no Retry-After metadata, so `EstimateRetryAfter` works it out from config.
- Decisions:
  - Middleware order: `UseAuthentication` → `UseAuthorization` → `UseRateLimiter`, as /create-api specifies
    (so the global limiter can partition by user). /implement-auth's text put the limiter before authentication.
  - `GlobalExceptionHandler` and `InvalidModelStateResponseFactory` added now so every error already has the BaseResponse shape.
    /create-api reuses them. MVC's implicit `[Required]` on non-nullable strings is switched off, so FluentValidation is the only validator.
  - Claims use short names (`MapInboundClaims = false`): `sub`, `email`, `name`, `role`, `jti`. Role/Name claim types are set to match.
  - `AddJwtAuthentication()` takes no `IConfiguration`: it configures bearer options from the validated `IOptions<JwtOptions>`.
- Known limitations (accepted):
  - An access token can't be revoked before it expires (≤ 15 min), e.g. after deactivation or logout.
  - Two simultaneous refreshes with the same cookie (two tabs) make the second look like reuse, which signs the user out everywhere.
  - CORS is not configured yet (/create-api). With the Angular dev proxy, the cookie is same-site and works.
  - Behind a reverse proxy, forwarded headers must be configured, or the stored IP and rate-limit partition will be the proxy's.
- User action: `dotnet user-secrets set "Jwt:SigningKey" "<64+ random characters>" -p src/SmartTask.Api`.
- Next: `/create-api` (it reuses the foundations above; it adds the migration, CORS, Swagger, Serilog and health checks).

### 2026-11-11 — /create-api: stopped, waiting for /implement-auth
- Not started: `/create-api` depends on `AddJwtAuthentication`, `CurrentUserService` and `AddInfrastructure()`
  from `/implement-auth` (step 5 in WORKFLOW.md). The user chose to run `/implement-auth` first.
- Fixed a `/create-features` bug found when its tests first ran (310 passed, 2 failed):
  `TaskService.CreateAsync`/`AssignAsync` reloaded the task and relied on EF to fill `Assignee`, which the mapping guard rejected.
  The service now passes the assignee it already loaded (`ResolveAssigneeAsync`) to a new
  `TaskMappings.ToDto(task, projectName, today, assignee)` overload. That removes the hidden EF dependency and one query.
- Tests: ⚠️ the re-run after the fix was blocked again by Windows Application Control, this time on the rebuilt
  `SmartTask.Application.UnitTests.dll`. The 53 Domain tests pass. Re-run `dotnet test SmartTask.slnx` to confirm.

### 2026-11-11 — /create-features (Users, Projects, Tasks, Dashboard)
- Created in `src/SmartTask.Application/`:
  - `Common/Responses/`: IBaseResponse, BaseResponse, BaseResponse<T> (as in API-CONVENTIONS §2), ResponseMessages
  - `Contracts/Services/`: IUserService, IProjectService, ITaskService, IDashboardService (one method per endpoint in §7, XML-documented with status codes)
  - `Services/Authorization/`: IProjectAccessGuard, ProjectAccessGuard
  - `Services/`: BusinessErrorHandling (DomainException → 400, ConflictException → 409), Users/UserService,
    Projects/ProjectService, Tasks/TaskService, Dashboard/DashboardService
  - `Common/Models/ProjectTaskCounts`
- Updated:
  - `AddApplication()` registers the guard and the 4 services (scoped); Application gets `Microsoft.Extensions.Logging.Abstractions` 10.0.14
  - `ITaskItemRepository` + impl: `GetByProjectAsync` (tracked, for deleting a project) and `GetCountsByProjectAsync` (one SQL GROUP BY).
    These close the two follow-ups from /create-dto.
  - `IDashboardReadRepository.GetStatsAsync` + impl: new `upcomingDays` parameter, clamped in both service and repository
  - Domain constants: `DashboardConstraints` (`UpcomingWindowDays` renamed to `DefaultUpcomingDays`; new `MinUpcomingDays` 1,
    `MaxUpcomingDays` 30); `PagingConstraints.LookupMaxResults` 20
- Tests: `tests/SmartTask.Application.UnitTests/Services/` (NSubstitute 6.2.0 for every contract, real ProjectAccessGuard,
  FakeTimeProvider, `Build` entity helpers): ProjectAccessGuardTests, ProjectServiceTests, TaskServiceTests, UserServiceTests, DashboardServiceTests.
- Build: ✅ (0 warnings, 0 errors).
- Tests: ⚠️ **not verified.** After this change, Windows Application Control blocks loading the rebuilt `SmartTask.Domain.dll` (0x800711C7).
  A TRX log shows all 157 failures are that load error and none is an assertion. The 114 tests that don't load Domain types pass.
  The previously green 53 Domain tests are blocked the same way. Re-run `dotnet test SmartTask.slnx` once the policy allows the DLL.
- Decisions:
  - Not visible → 404, visible but not allowed → 403, everywhere. A task is visible when its project is.
  - Managing a project needs Admin, or ProjectManager **and** owner. A demoted owner loses management rights.
  - An assignee may change status only while still a project member. Assignees can't edit details (403).
  - The creator isn't stored as a member row: the domain already counts the owner as a member (`Project.HasMember`).
  - Delete project: loads its tasks and removes them with the project in one `SaveChangesAsync`.
  - Admins can't demote or deactivate themselves (400). Deactivation revokes active refresh tokens.
  - Admin-only and manager-only checks are repeated in the services as defence in depth (the API policies are the first line).
  - Dashboard ProjectManager scope = **owned** projects (CLAUDE.md §4), not "own/member" as in the command text.
- Open questions:
  - Removing a member leaves tasks still assigned to them. Should they be unassigned automatically?
  - Dashboard PM scope: confirm owned projects only (CLAUDE.md) vs owned + member (command text).

### 2026-11-11 — /implement-validators (all)
- Packages: `FluentValidation` + `FluentValidation.DependencyInjectionExtensions` 12.2.0 in Application (no AspNetCore auto-validation).
- Created in `src/SmartTask.Application/`:
  - `DependencyInjection.cs`: `AddApplication()` registers all validators (`includeInternalTypes: true`) and `TryAddSingleton(TimeProvider.System)`
  - `Common/Validation/ValidationResultExtensions`: `ToErrorDictionary()` with camelCase property paths, for the API's ValidationFilter
  - `Validators/Common/`: RuleBuilderExtensions (NotBlank, TrimmedLength, TrimmedMaxLength, ValidPersonName, ValidEmail,
    ValidPassword, NotInPast, OnOrAfter, ValidSortField), PageQueryValidator<T>, PersonNameValidator<T>
  - `Validators/Auth/`: RegisterRequestDtoValidator, LoginRequestDtoValidator
  - `Validators/Users/`: UpdateUserProfileDtoValidator, ChangeUserRoleDtoValidator, UserQueryParametersValidator
  - `Validators/Projects/`: ProjectDetailsValidator<T>, Create/UpdateProjectDtoValidator, AddProjectMemberDtoValidator, ProjectQueryParametersValidator
  - `Validators/Tasks/`: TaskDetailsValidator<T>, Create/UpdateTaskDtoValidator, UpdateTaskStatusDtoValidator, AssignTaskDtoValidator, TaskQueryParametersValidator
  - `Validators/Ai/`: ImproveDescriptionRequestDtoValidator
- Updated DTOs: added `IProjectDetails`, `ITaskDetails` and `IPersonName` (read-only interfaces). Create/Update/Register
  DTOs implement them, so each set of shared rules lives in one abstract validator instead of being copied.
- Created `tests/SmartTask.Application.UnitTests` (xUnit, Shouldly, FluentValidation.TestHelper, FakeTimeProvider; added to `/tests/`):
  one file per validator, plus ValidationResultExtensionsTests and DependencyInjectionTests (every request DTO has exactly
  one registered validator, and a new request DTO without one fails the test).
- Build: ✅ (0 warnings) / Tests: ✅ 232 passed (53 Domain + 179 Application).
- Decisions:
  - Length rules measure the **trimmed** value (the domain trims before storing), so `"  ab  "` fails a 3-character minimum.
  - `RuleLevelCascadeMode = Stop` everywhere: one message per field at a time (e.g. password rules report the first unmet requirement).
  - Login validates only Email format and Password max length (no complexity, as required).
  - Password must not contain the email local part, checked only when the local part is ≥ 3 characters (`MinLocalPartLengthToCompare`).
  - Create project: StartDate may be up to 1 day in the past (`StartDateToleranceDays`). The rule is skipped when StartDate is missing, to avoid a duplicate error.
  - `SortBy` that is blank or only whitespace is accepted (the repository then uses the default sort).
- Open questions:
  - Should the password rules report all unmet requirements at once instead of one at a time?
  - Person names: digits are rejected. Confirm that's wanted (e.g. "John Smith 3rd").

### 2026-11-10 — /create-dto (all)
- Created in `src/SmartTask.Application/DTOs/`:
  - `Users/`: UserDto, UserSummaryDto, UpdateUserProfileDto, ChangeUserRoleDto, UserMappings
  - `Auth/`: RegisterRequestDto, LoginRequestDto, AuthResponseDto, AuthRedaction (internal)
  - `Projects/`: CreateProjectDto, UpdateProjectDto, ProjectDto, ProjectSummaryDto, ProjectMemberDto, AddProjectMemberDto, ProjectMappings
  - `Tasks/`: CreateTaskDto, UpdateTaskDto, UpdateTaskStatusDto, AssignTaskDto, TaskDto, TaskSummaryDto, TaskMappings
  - `Dashboard/`: DashboardStatsDto, DashboardMappings
  - `Ai/`: ImproveDescriptionRequestDto, ImproveDescriptionResponseDto
- Build: ✅ (0 warnings) / Tests: ✅ (53 passed). The sensitive-field grep prints nothing. A scratch check (not committed)
  covered ToString redaction, IsOverdue, the missing-navigation guard and dashboard enum filling: 10/10 passed.
- Decisions:
  - `ProjectMappings.ToDto(project, taskCount, completedTaskCount)`: `GetDetailsAsync` doesn't load tasks, so the counts are parameters.
  - `TaskMappings.ToDto(task, projectName, today)`: TaskItem has no Project navigation (decided in step 2), so the caller passes the name.
  - Mappings throw `InvalidOperationException` if a promised navigation (Owner, Members.User, a set Assignee) wasn't loaded,
    instead of returning empty names.
  - `ProjectDto.Members` lists explicit members only; the owner is in `Owner`. `MemberCount` = members + owner.
  - `AuthResponseDto.ToString()` also redacts `AccessToken` (the command only required redacting passwords).
  - `UpdateTaskDto.Priority` defaults to `Medium`, the same as on create.
- Open questions / follow-ups for `/create-features`:
  - `IProjectRepository` has no task-count query for `ProjectDto`. Add one (e.g. `GetTaskCountsAsync(projectId)`) rather than loading tasks.
  - API-CONVENTIONS has `GET dashboard?upcomingDays=7`, but `IDashboardReadRepository.GetStatsAsync` uses the fixed
    `DashboardConstraints.UpcomingWindowDays`. Either add a parameter or drop the query option.
  - The overdue rule is written three times: `TaskItem.IsOverdue`, the SQL predicate, and `UpcomingTaskReadModel` mapping.

### 2026-11-10 — /create-repositories (all)
- Created projects: `src/SmartTask.Application` (references Domain only, no packages) and
  `src/SmartTask.Persistence` (EF Core SqlServer + Design 10.0.14), both added to `SmartTask.slnx` under `/src/`.
- Created in `SmartTask.Application`:
  - `Contracts/Persistence/`: IRepository, IUserRepository, IRefreshTokenRepository, IProjectRepository,
    ITaskItemRepository, IDashboardReadRepository, IUnitOfWork
  - `Contracts/Identity/ICurrentUserService`
  - `Common/Models/`: PagedResult, PageQuery, SortDirection, Project/Task/UserQueryParameters
    (each with `SortFields` constants and `AllowedSortFields`), ProjectListItem, UpcomingTaskReadModel, DashboardStatsReadModel
  - `Common/Exceptions/ConflictException`
- Created in `SmartTask.Persistence`: SmartTaskDbContext, UnitOfWork, DependencyInjection (`AddPersistence`);
  `Configurations/` (5 entities); `Interceptors/` (SoftDelete, AuditableEntity); `Extensions/` (QueryableExtensions,
  AccessQueryExtensions); `Repositories/` (Repository<T> + 5 specific repositories).
- Updated Domain: `RefreshTokenConstraints.TokenHashMaxLength` (128; a unique index can't use nvarchar(max));
  new `DashboardConstraints` (UpcomingWindowDays 7, UpcomingTaskCount 5).
- Build: ✅ (0 warnings, 0 errors) / Tests: ✅ (53 passed). A throwaway LocalDB smoke test (scratchpad, not committed)
  ran every repository method, the interceptors, the RowVersion and unique-index conflicts, the transaction and soft delete: 40/40 passed.
- Decisions / deviations:
  - **No `User` navigation on RefreshToken and no `Project` navigation on TaskItem** (user's decision: the spec
    doesn't define them). `GetByTokenHashAsync` returns the token only, so the caller loads the user by `UserId`.
    `TaskItemRepository.GetDetailsAsync` includes only `Assignee`.
  - Ids are `ValueGeneratedNever()` for every BaseEntity. Without it, EF treated new child entities that already
    had an Id (e.g. `User.AddRefreshToken`) as existing rows and issued UPDATEs (found by the smoke test).
  - `CascadeDeleteTiming = OnSaveChanges`, so a soft-deleted project doesn't hard-delete its tracked members/tasks.
    Task queries always join to non-deleted projects, so tasks of a deleted project are hidden even though their own
    `IsDeleted` stays false.
  - `ApplySorting` takes `SortDirection?`: null means asc for an explicit key and the default direction (desc) for the fallback.
  - The UoW also maps unique-index violations (SQL 2601/2627) to `ConflictException`.
  - `ProjectListItem.MemberCount` includes the owner (matches `Project.HasMember`).
  - Dashboard scopes: Admin = all; ProjectManager = owned projects and their tasks; TeamMember = projects they belong to
    plus tasks assigned to them.
  - Pinned `System.Security.Cryptography.Pkcs` 10.0.14 in Persistence: SqlClient 6.1.7 pulls 9.0.11 (NU1902/NU1903).
- Next: `dotnet ef migrations add InitialCreate -p src/SmartTask.Persistence -s src/SmartTask.Api` once the Api project exists.
- Open questions: none blocking. Repository integration tests (Testcontainers) are still to do.

### 2026-11-10 — /create-domain (all), re-run to verify
- Created: nothing. Updated: `docs/PROGRESS.md` only. The existing Domain code was checked against
  `docs/DOMAIN-MODEL.md` and matches it; the known deviations are listed in the first `/create-domain` entry below.
- Build: ✅ (0 warnings, 0 errors) / Tests: ✅ (53 passed). The Application Control block from the first run no longer occurs.
- Self-check: no project or package references, no public setters, no `DateTime*.Now`/`UtcNow`,
  no EF, DataAnnotations or serialization attributes.
- Open questions: none new.

### 2026-11-10 — Restructure: solution moved to repo root
- Moved `TaskPlus/SmartTask.slnx`, `TaskPlus/Directory.Build.props`, `TaskPlus/src/` and `TaskPlus/tests/`
  to the repo root, then deleted the `TaskPlus/` folder (including its `.vs/` cache).
- Updated: `SmartTask.slnx` (removed the stale commented-out `TaskPlus.csproj` entry);
  `CLAUDE.md` (root layout in §3 plus a layout rule; `SmartTask.sln` → `SmartTask.slnx`);
  `.claude/commands/*.md` (`SmartTask.sln` → `SmartTask.slnx`; `create-domain` now creates an `slnx`).
- New projects go in `src/<Project>/` and test projects in `tests/<Project>/`.

### 2026-11-10 — /create-domain (all)
- Paths are relative to the repo root. The solution was later moved from `TaskPlus/` to the root (see the restructure entry above).
- Created: `Directory.Build.props`;
  `src/SmartTask.Domain/` — `Common/` (BaseEntity, AuditableEntity, ISoftDeletable, internal Guard),
  `Constants/` (User, Project, TaskItem, Paging, Ai, RefreshToken constraints),
  `Entities/` (User, RefreshToken, Project, ProjectMember, TaskItem),
  `Enums/` (UserRole, TaskItemStatus, TaskPriority),
  `Exceptions/` (DomainException, InvalidStatusTransitionException);
  `tests/SmartTask.Domain.UnitTests/` (xUnit + Shouldly) — TaskItemTests, ProjectTests, UserTests, RefreshTokenTests.
- Updated: `SmartTask.slnx` (added both projects).
- Build: ✅ (0 warnings, 0 errors) / Tests: ⚠️ not run. Windows Application Control blocked loading
  `SmartTask.Domain.UnitTests.dll` (0x800711C7). Re-run `dotnet test` once the policy allows local build output.
- Deviations from `docs/DOMAIN-MODEL.md` (all needed so the domain never reads the clock):
  - `User.ChangePasswordHash(newHash, DateTimeOffset now)`: `now` added, because revoking active tokens needs the current time.
  - `Project.AddMember(userId, DateTimeOffset joinedAt)`: `joinedAt` added, because `ProjectMember.JoinedAt` must be set.
  - Added `RefreshTokenConstraints` (CreatedByIp 45, RevokedReason 200) so these lengths are not magic numbers.
- Design decisions:
  - The owner counts as a member (`HasMember` returns true for the owner) without a `ProjectMember` row. `AddMember(owner)` does nothing.
  - `ProjectMember.Create` is `internal`, so members can only be added through `Project.AddMember`.
  - `RefreshToken.Revoke` throws if the token is already revoked. `RefreshToken.Create` requires `expiresAt > createdAt`.
  - Added `TaskItem.CanTransitionTo` and `RefreshToken.IsRevoked` (computed, not persisted).
- Open questions / follow-ups:
  - "DueDate not in the past on create" and "assignee must be a project member" need the clock or project data,
    so they are left to the validators/handlers.
  - ~~Template `TaskPlus/src/TaskPlus.csproj` overlaps `src/SmartTask.Domain`~~. Resolved: the template is gone.
