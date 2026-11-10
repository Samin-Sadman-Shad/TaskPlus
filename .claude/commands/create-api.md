---
description: Create thin REST controllers on a shared BaseApiController plus the API cross-cutting setup (Swagger, Serilog, exception handling, CORS, rate limiting, health checks)
argument-hint: "[Users | Projects | Tasks | Dashboard | Setup]  (optional — omit for all controllers + setup)"
allowed-tools: Read, Write, Edit, Glob, Grep, Bash(dotnet new:*), Bash(dotnet sln:*), Bash(dotnet add:*), Bash(dotnet build:*), Bash(dotnet test:*), Bash(dotnet ef:*), Bash(ls:*), Bash(mkdir:*)
---

# /create-api

Expose the application services as REST endpoints and finish the API host. Controllers are **thin**:
every action calls one service method and converts the `BaseResponse` with `ToActionResult` from
`BaseApiController`.

**Scope:** `$ARGUMENTS` — `Setup` for the cross-cutting host configuration only, a feature name for
one controller, or empty for everything.

## 0. Read context first

1. `CLAUDE.md`
2. `docs/API-CONVENTIONS.md` — **the whole file**: base controller code, error handling, policies,
   rate limits and the endpoint catalogue
3. `docs/CONVENTIONS.md` — API section
4. Existing code: `src/SmartTask.Api/`, `Application/Contracts/Services/`, `Common/Responses/`

Prerequisite: the service interfaces from `/create-features` (and `/implement-auth`). If a controller's
service is missing, skip that controller and report it.

## 1. Base controller and filter

If they don't exist, create them exactly as in API-CONVENTIONS §3–§4:
- `Controllers/BaseApiController.cs` — `ToActionResult<TResponse>` and `ToCreatedResult<T>`
- `Filters/ValidationFilter.cs`
- `Authorization/Policies.cs`, `RateLimiting/RateLimitPolicies.cs` (string constants)

If they already exist (created by `/implement-auth`), reuse them and don't duplicate.

## 2. Controllers

One controller per feature in `src/SmartTask.Api/Controllers/`. Every controller:
- `public sealed class XController(IXService xService) : BaseApiController`, with **only** the service
  interface injected
- `[Route("api/v1/...")]` taken exactly from API-CONVENTIONS §7
- uses role policies from §5 on the actions that need them (`[Authorize(Policy = Policies.ProjectManagement)]`);
  resource checks stay in the service
- each action is **a single expression**:
  ```csharp
  => ToActionResult(await taskService.UpdateStatusAsync(id, dto, cancellationToken));
  ```
  `POST` creates use `ToCreatedResult(..., nameof(GetById), x => new { id = x.Id })`
- binds ids from the route (`{id:guid}`), bodies with implicit `[FromBody]`, and lists with
  `[FromQuery] ProjectQueryParameters parameters`
- takes `CancellationToken cancellationToken` as the last parameter
- has `[ProducesResponseType<...>]` for **every** status code the service can return, plus 401/429
- has an XML `<summary>` per action (it shows up in Swagger)

**Forbidden in controllers:** `try/catch`, `if` on results, repositories, `DbContext`, mapping,
`User.FindFirst(...)` (use `ICurrentUserService` in the services), and business rules.

Controllers to create: `UsersController`, `ProjectsController`, `TasksController` (routes under both
`projects/{projectId}/tasks` and `tasks`) and `DashboardController`. `AuthController` comes from
`/implement-auth` and `AiController` from `/implement-ai`. If `AuthController` exists, check that it
follows the same rules.

## 3. Host setup (`Setup` scope)

Keep `Program.cs` under ~60 lines. Put the configuration in `Extensions/`:

```
SmartTask.Api/
├── Extensions/
│   ├── ServiceCollectionExtensions.cs   AddApiServices(config, env)
│   ├── SwaggerExtensions.cs
│   ├── RateLimitingExtensions.cs
│   ├── CorsExtensions.cs
│   ├── HealthCheckExtensions.cs
│   └── WebApplicationExtensions.cs      UseApiPipeline()
├── Middleware/
│   ├── GlobalExceptionHandler.cs        IExceptionHandler
│   └── SecurityHeadersMiddleware.cs
└── Program.cs
```

| Concern | Implementation |
|---|---|
| **Logging** | `Serilog.AspNetCore`: `builder.Host.UseSerilog((ctx, sp, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration).ReadFrom.Services(sp).Enrich.FromLogContext())`. Configure Console + rolling File sinks in `appsettings.json`. Add `UseSerilogRequestLogging()` and enrich with `UserId` and `TraceId`. Bootstrap logger in `Program.cs` with `try/catch/finally Log.CloseAndFlush()` |
| **Exceptions** | `GlobalExceptionHandler`: `DomainException` → 400, `ConflictException` → 409, everything else → 500 with the generic message "An unexpected error occurred." Always logged with the trace id. Writes the `BaseResponse` JSON. Register with `AddExceptionHandler<GlobalExceptionHandler>()` + `AddProblemDetails()` + `UseExceptionHandler()` |
| **Validation** | Global `ValidationFilter`. `ApiBehaviorOptions.InvalidModelStateResponseFactory` returns `BaseResponse.ValidationFailed` for binding errors |
| **JSON** | camelCase, `JsonStringEnumConverter`, `DefaultIgnoreCondition = WhenWritingNull` |
| **Swagger** | `Swashbuckle.AspNetCore` (or `Microsoft.AspNetCore.OpenApi` + Scalar/Swagger UI — pick one and note it). Title, version `v1`, XML comments (`<GenerateDocumentationFile>true</GenerateDocumentationFile>`), JWT bearer security scheme + requirement so "Authorize" works. Enable the UI only in Development **or** when the `Swagger:Enabled` setting is true |
| **CORS** | Named policy `Frontend`. Origins come from `Cors:AllowedOrigins` (array) in config — never `AllowAnyOrigin`. `AllowCredentials()` (needed for the refresh cookie), the specific methods used, and the `Content-Type` and `Authorization` headers |
| **HTTPS** | `UseHttpsRedirection()`; `UseHsts()` outside Development |
| **Security headers** | `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'` for API responses (skip the CSP on the Swagger UI path) |
| **Rate limiting** | `global`, `auth` and `ai` policies from API-CONVENTIONS §6, with limits bound from the `RateLimiting` config section. `OnRejected` → 429 `BaseResponse` + `Retry-After` |
| **Auth** | Reuse `AddJwtAuthentication` from `/implement-auth`. Fallback policy = authenticated user |
| **Health checks** | `AddHealthChecks().AddDbContextCheck<SmartTaskDbContext>(tags: ["ready"])`. Map `/health/live` (no checks) and `/health/ready` (tag `ready`), both `[AllowAnonymous]` + `DisableRateLimiting()`, with a JSON response writer |
| **Composition** | `builder.Services.AddApplication().AddPersistence(config).AddInfrastructure(config).AddApiServices(config, env)` |
| **Request limits** | `Kestrel.Limits.MaxRequestBodySize = 1 MB` |

Middleware order in `UseApiPipeline()`:
```
UseExceptionHandler → UseHsts (non-dev) → UseHttpsRedirection → SecurityHeaders
→ UseSerilogRequestLogging → UseSwagger (if enabled) → UseCors("Frontend")
→ UseAuthentication → UseAuthorization → UseRateLimiter
→ MapControllers (RequireRateLimiting global) → MapHealthChecks
```
`UseRateLimiter` comes **after** `UseAuthentication`, so the global limiter can partition by user id.

### Configuration files
- `appsettings.json`: Serilog, `Jwt` (no key), `Cors:AllowedOrigins: ["http://localhost:4200"]`,
  `RateLimiting`, `Swagger:Enabled: false`, `ConnectionStrings:DefaultConnection` pointing at local
  SQL Server **without a password** (integrated security / LocalDB), `AllowedHosts`.
- `appsettings.Development.json`: `Swagger:Enabled: true`, more verbose logging.
- Launch profile: HTTPS on a fixed port (e.g. `https://localhost:7180`) so the Angular proxy can target it.

### Database
If no migration exists yet, run
`dotnet ef migrations add InitialCreate -p src/SmartTask.Persistence -s src/SmartTask.Api -o Migrations`.
Add an **optional** development-only seeder (`Persistence/Seed/DevelopmentDataSeeder.cs`) that creates
one Admin, one ProjectManager and one TeamMember with passwords from user-secrets, plus two sample
projects. It runs only when `Seed:Enabled` is true **and** the environment is Development. Don't
auto-migrate in production.

## 4. Tests

Add `tests/SmartTask.Api.IntegrationTests` (xUnit + `Microsoft.AspNetCore.Mvc.Testing`) with a
`WebApplicationFactory` that swaps SQL Server for the EF Core in-memory **SQLite** provider. Cover:
- `/health/live` → 200
- an anonymous request to a protected endpoint → 401 in `BaseResponse` shape
- an invalid body → 400 with `errors`
- `BaseApiController.ToActionResult` mapping for each status (unit test with a stub controller),
  including that **204 has no body** and an unknown status passes through unchanged

## 5. Verify

```bash
dotnet build SmartTask.sln
dotnet test SmartTask.sln
grep -rnE "try\s*\{|DbContext|Repository" src/SmartTask.Api/Controllers   # must print nothing
```

Then run the API and check `/swagger` loads and `/health/ready` returns Healthy (ask the user to
start SQL Server if it isn't reachable).

Self-check, report ✅/❌:
- [ ] Every endpoint in API-CONVENTIONS §7 within scope exists with the exact route, verb and policy
- [ ] All controllers inherit `BaseApiController`, inject only service interfaces, and have one-line actions
- [ ] Every error path returns the `BaseResponse` shape (validation, binding, 401, 403, 429, 500)
- [ ] CORS origins from config, credentials allowed, no wildcard
- [ ] Swagger has JWT auth and XML comments
- [ ] Serilog request logging and health checks are live
- [ ] No secrets in `appsettings*.json`

## 6. Finish

1. Update `docs/PROGRESS.md`.
2. Print the endpoint table (verb, route, policy) and how to run the API.
3. Suggested commit: `feat(api): thin controllers, base controller mapping and host configuration`.
