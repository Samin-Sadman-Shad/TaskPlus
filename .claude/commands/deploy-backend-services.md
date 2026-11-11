---
description: Containerise the API (two-stage Dockerfile) and run it with SQL Server and a persistent data volume through Docker Compose, with Swagger + JWT over HTTP and HTTPS, ready for the Angular client
argument-hint: "[Database | Swagger | Docker]  (optional — omit for everything)"
allowed-tools: Read, Write, Edit, Glob, Grep, Bash(dotnet build:*), Bash(dotnet test:*), Bash(dotnet publish:*), Bash(dotnet dev-certs:*), Bash(docker:*), Bash(docker compose:*), Bash(netstat:*), Bash(curl:*), Bash(ls:*), Bash(mkdir:*)
---

# /deploy-backend-services

Make the backend deployable. The API is built by a **two-stage Dockerfile** and runs next to a
**SQL Server container**. The database files live in a host folder, so they survive
`docker compose down`. **Docker Compose** starts everything, and leaves a slot for the Angular
client (`client/smart-task-web`). When this command is done, the client can call the API on
`localhost` over HTTP and HTTPS, and Swagger UI can be used to test every endpoint with a JWT.

**Scope:** `$ARGUMENTS`
- `Database`: connection-string handling, startup migrations, SQL Server service, init script, volume
- `Swagger`: schema ids, auth metadata
- `Docker`: Dockerfile, `.dockerignore`, compose file, `.env`, certificates
- empty: all three, in that order

## 0. Read context first

1. `CLAUDE.md`: especially §6 rule 7 (**no secrets in source**) and §3 (dependency rule)
2. `docs/API-CONVENTIONS.md`: §5 (CORS, rate limits) and the health endpoints in §7
3. `src/SmartTask.Api/Properties/launchSettings.json`: **the host ports come from here**
   (`http` profile → HTTP port, `https` profile → HTTPS port; currently 5180 / 7180)
4. `src/SmartTask.Api/appsettings.json` and `appsettings.Development.json`: config keys
   (`ConnectionStrings:DefaultConnection`, `Jwt`, `Cors:AllowedOrigins`, `Swagger:Enabled`,
   `Seed:Enabled`, `Ai`, `Serilog`)
5. Existing code: `Program.cs`, `Extensions/WebApplicationExtensions.cs`,
   `Extensions/SwaggerExtensions.cs`, `Extensions/CorsExtensions.cs`,
   `SmartTask.Persistence/DependencyInjection.cs`, `SmartTask.Persistence/SmartTaskDbContext.cs`,
   `Seed/DevelopmentDataSeeder.cs`
6. `Directory.Build.props`: `TreatWarningsAsErrors` is on, so the container build fails on any warning

Prerequisites: `/create-api` and `/implement-auth` are done. **Docker Desktop** (WSL 2 backend) is
installed and running. Check with `docker ps`; an error mentioning `dockerDesktopLinuxEngine` means
the engine isn't up. If it's missing or stopped, **stop and ask the user** to install it from
docker.com or start it, and wait for "Engine running".

## 1. Fixed values (from the user)

| Setting          | Value |
|------------------|-------|
| Database name    | `SmartTaskDb` |
| App login        | `Samin` |
| App password     | `Samin` (goes into `.env` only, never into a tracked file) |
| SQL Server port  | `1433` (the default) |
| Data folder      | `D:\Projects\TaskPlus\sqlserver-data` (the parent of the repo, so it is never committed) |
| Host HTTP port   | `http` profile port in `launchSettings.json` (5180) |
| Host HTTPS port  | `https` profile port in `launchSettings.json` (7180) |
| Host name        | `localhost` |

**Port check before choosing:** run `netstat -ano | findstr ":<port> "` for 5180, 7180 and 1433. If
a port is in use, take the next free port (5181…, 7181…, 1434…), write it to `.env`, and report the
change. Never stop a process that holds a port.

**Password policy:** `Samin` doesn't meet SQL Server's password policy, and the container's `sa`
account *requires* a complex password. So:
- `sa` gets a **generated** strong password (`MSSQL_SA_PASSWORD` in `.env`) and is used **only** by
  the init script;
- the app connects as `Samin`, created by the init script with `CHECK_POLICY = OFF`.
In the report, say that `Samin`/`Samin` is only acceptable for local development.

## 2. Persistence (`Database` scope)

### `SmartTask.Persistence/DependencyInjection.cs`
- **Password injection.** Keep reading `ConnectionStrings:DefaultConnection`. If a
  `Database:Password` value exists, apply it with `SqlConnectionStringBuilder`
  (`builder.Password = …`). Then the connection string in `appsettings.Docker.json` has no secret,
  and the password arrives as the `Database__Password` environment variable. If the value is
  missing, use the string unchanged, so LocalDB / Trusted_Connection keeps working.
- **Retry.** Bind `EnableRetryOnFailure(maxRetryCount, maxRetryDelay, null)` from
  `Database:MaxRetryCount` (default 6) and `Database:MaxRetryDelaySeconds` (default 30). The
  container can accept TCP connections before it accepts logins.
- Startup migrations are **not** done here. They belong to the host (see below).

### `SmartTask.Persistence/SmartTaskDbContext.cs`
- Confirm it has **no** `OnConfiguring` and no connection string. Remove them if present: all
  configuration comes from DI. Docker-specific code doesn't belong in the DbContext. If nothing
  needs to change, say so in the report; don't invent a change.

### Migration readiness (run before anything Docker-related)
Check that Domain, Persistence and the migrations agree, then bring the local database up to date:
```bash
dotnet tool restore                                                                     # dotnet-ef from dotnet-tools.json
dotnet build SmartTask.slnx
dotnet ef migrations has-pending-model-changes -p src/SmartTask.Persistence -s src/SmartTask.Api --no-build
#   → "No changes have been made to the model since the last migration."
dotnet ef migrations list -p src/SmartTask.Persistence -s src/SmartTask.Api --no-build  # "(Pending)" marks unapplied ones
dotnet ef database update -p src/SmartTask.Persistence -s src/SmartTask.Api --no-build  # → "Done."
```
If `has-pending-model-changes` reports changes, the model has moved on since the last migration.
**Stop and ask** before adding one: show the diff (`dotnet ef migrations add <Name>` into a scratch
branch, or `script`), and check the change against `docs/DOMAIN-MODEL.md`. Never edit or delete an
applied migration. To review the SQL the container will run:
`dotnet ef migrations script --idempotent -p src/SmartTask.Persistence -s src/SmartTask.Api -o <scratchpad>/migrations.sql`.

### Startup migrations: `SmartTask.Api/Extensions/DatabaseMigrationExtensions.cs`
`await app.ApplyDatabaseMigrationsAsync();` is called in `Program.cs` right after `UseApiPipeline()`
and before `SeedDevelopmentDataAsync()`, so the schema exists before Kestrel accepts requests. Check
that it is present and behaves like this (create it if it's missing):
- `ShouldApplyMigrations(configuration)`: `Database:ApplyMigrationsOnStartup` wins when it's set.
  Otherwise migrate only when `DOTNET_RUNNING_IN_CONTAINER` is `true`, which the official .NET images
  set. So `docker run` and `docker compose up` both migrate, and a plain `dotnet run` never does.
- `GetPendingMigrationsAsync`, then log the count and names at Information (no connection
  details), then `Database.MigrateAsync(app.Lifetime.ApplicationStopping)`. EF Core takes a
  migration lock, so parallel replicas are safe.
- Failures are **not** caught. The host logs Fatal and exits with code 1, and the compose restart
  policy retries.
- `tests/SmartTask.Api.IntegrationTests/SmartTaskApiFactory.cs` sets
  `Database:ApplyMigrationsOnStartup=false`, because the migrations are SQL Server-specific and the
  tests use SQLite. `DatabaseMigrationExtensionsTests` covers the decision table.
- `Program.cs` stays a short composition root: any extra wiring goes in an `Extensions/` class.

### `SmartTask.Api/appsettings.Docker.json` (new, no secrets)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=sqlserver,1433;Database=SmartTaskDb;User Id=Samin;Encrypt=True;TrustServerCertificate=True"
  },
  "Database": { "ApplyMigrationsOnStartup": true },
  "Swagger": { "Enabled": true },
  "Cors": { "AllowedOrigins": [ "http://localhost:4200", "https://localhost:4200" ] }
}
```
`TrustServerCertificate=True` is acceptable here because the traffic stays on the private compose
network with a self-signed SQL certificate. Add a comment about this in the report, not in the JSON.
Make sure the csproj copies it on publish (it does by default for `appsettings*.json`).

### `docker/sqlserver/init.sql` (new, idempotent)
Run by a one-shot `sqlserver-init` service as `sa`, using sqlcmd variables
(`$(APP_DB_NAME)`, `$(APP_DB_USER)`, `$(APP_DB_PASSWORD)`), so no value is hard-coded:
1. `IF DB_ID('$(APP_DB_NAME)') IS NULL CREATE DATABASE [$(APP_DB_NAME)];`
2. `IF SUSER_ID('$(APP_DB_USER)') IS NULL CREATE LOGIN [$(APP_DB_USER)] WITH PASSWORD = N'$(APP_DB_PASSWORD)', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF;`
3. In the database: create the user if it's missing, then `ALTER ROLE db_owner ADD MEMBER`
   (needed so EF can apply migrations).

## 3. Swagger (`Swagger` scope), in `Extensions/SwaggerExtensions.cs`

1. **Generic schema ids.** Add the two helpers below as `private static` methods and register them
   with `options.CustomSchemaIds(GenerateSchemaId);`. Schemas are then named
   `BaseResponse<UserDto>` and `PagedResult<TaskSummaryDto>`, not `UserDtoBaseResponse`. Use
   `System.Text`, `string.Concat`/`StringBuilder` and `IndexOf('`', StringComparison.Ordinal)`, so
   the analyzers don't raise warnings, which would be errors here:
   ```csharp
   private static string GenerateSchemaId(Type type)
   {
       if (!type.IsGenericType)
       {
           return type.Name;
       }

       var sb = new StringBuilder(RemoveGenericTypeParameterCount(type.GetGenericTypeDefinition().Name)).Append('<');
       var arguments = type.GetGenericArguments();
       for (var i = 0; i < arguments.Length; i++)
       {
           if (i > 0)
           {
               sb.Append(',');
           }

           sb.Append(GenerateSchemaId(arguments[i]));
       }

       return sb.Append('>').ToString();
   }

   /// <summary>"BaseResponse`1" → "BaseResponse".</summary>
   private static string RemoveGenericTypeParameterCount(string typeName)
   {
       var backtick = typeName.IndexOf('`', StringComparison.Ordinal);
       return backtick >= 0 ? typeName[..backtick] : typeName;
   }
   ```
   Note in the XML doc: `<`, `>` and `,` fall outside the OpenAPI component-name pattern. Swagger
   UI handles them, but some importers (code generators, Postman) may not. `/write-docs` must
   check the exported collection still imports.
2. **Authentication and authorization in the UI.** Replace the global security requirement with an
   `IOperationFilter` (`Swagger/AuthorizeOperationFilter.cs`):
   - endpoints with `[AllowAnonymous]` (Auth, health) get **no** padlock;
   - every other endpoint gets the Bearer requirement, plus a line in its description listing
     the required roles, read from the `[Authorize(Roles = …)]` attributes on the action and the
     controller (narrowing role first);
   - the filter reads metadata only; it never changes the authorization behaviour.
3. Keep XML comments and the `Bearer` scheme. Swagger is served when `Swagger:Enabled` is true
   (`appsettings.Docker.json` sets it).
4. Swagger UI has to be used over **HTTPS** (`https://localhost:7180/swagger`): the refresh cookie
   is `Secure`, and HTTP is redirected to HTTPS.

## 4. Docker (`Docker` scope)

### `src/SmartTask.Api/Dockerfile` (build context = repo root)
- **Stage `build`**: `mcr.microsoft.com/dotnet/sdk:10.0`, `WORKDIR /src`.
  1. Copy `SmartTask.slnx`, `Directory.Build.props` and only the five `src/*/*.csproj` files, then
     `dotnet restore src/SmartTask.Api/SmartTask.Api.csproj`. This gives a cached restore layer.
  2. Copy `src/` and run
     `dotnet publish src/SmartTask.Api/SmartTask.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false`.
  Tests aren't copied or run in the image. They run in step 5 before the image is built.
- **Stage `final`**: `mcr.microsoft.com/dotnet/aspnet:10.0`, `WORKDIR /app`.
  - `RUN mkdir -p /app/logs && chown $APP_UID /app/logs`: Serilog's file sink writes to `logs/`
    and the app doesn't run as root.
  - `COPY --from=build /app/publish .`, `USER $APP_UID`, `EXPOSE 8080 8081`,
    `ENTRYPOINT ["dotnet", "SmartTask.Api.dll"]`.

### `.dockerignore` (repo root)
`**/bin`, `**/obj`, `**/logs`, `tests/`, `client/`, `docs/`, `.git`, `.vs`, `.vscode`, `.claude`,
`.env`, `*.pfx`, `**/*.user`, `**/secrets.json`.

### HTTPS certificate
Kestrel in the container needs a certificate file:
```bash
dotnet dev-certs https -ep "$USERPROFILE/.aspnet/https/smarttask-api.pfx" -p "<CERT_PASSWORD from .env>"
dotnet dev-certs https --trust     # opens a Windows prompt: ask the user to accept it
```
The `.pfx` stays outside the repo and is mounted read-only. `*.pfx` must be in `.gitignore`.

### `.env` (git-ignored) and `.env.example` (committed, placeholders only)
```
COMPOSE_PROJECT_NAME=smarttask
API_HTTP_PORT=5180
API_HTTPS_PORT=7180
SQL_PORT=1433
SQLSERVER_DATA_DIR=D:/Projects/TaskPlus/sqlserver-data
MSSQL_SA_PASSWORD=<generated: 24+ chars, upper, lower, digit, symbol>
APP_DB_NAME=SmartTaskDb
APP_DB_USER=Samin
APP_DB_PASSWORD=Samin
JWT_SIGNING_KEY=<generated: 64+ random chars>
CERT_PASSWORD=<generated>
CERT_DIR=C:/Users/<you>/.aspnet/https
AI_API_KEY=
BOOTSTRAP_ADMIN_FIRST_NAME=<first name>
BOOTSTRAP_ADMIN_LAST_NAME=<last name>
BOOTSTRAP_ADMIN_EMAIL=<admin email>
BOOTSTRAP_ADMIN_PASSWORD=<strong password>
```
The bootstrap admin is personal data plus a password, so it lives in `.env` only. Ask the user for the
values when `.env` doesn't have them yet; leaving the email empty skips the bootstrap.
Generate the secrets. Never print them in the report; say only that they were generated. Check
`git check-ignore .env` before writing it.

### `docker-compose.yml` (repo root)
One network, `smarttask-network`. The services:

1. **`sqlserver`**: `mcr.microsoft.com/mssql/server:2022-latest`
   - env: `ACCEPT_EULA=Y`, `MSSQL_SA_PASSWORD=${MSSQL_SA_PASSWORD}`, `MSSQL_PID=Developer`
   - ports: `"${SQL_PORT}:1433"` (so SSMS can connect at `localhost,1433` as `Samin`)
   - volumes: `sqlserver-data` → `/var/opt/mssql/data`, `sqlserver-log` → `/var/opt/mssql/log`,
     `sqlserver-secrets` (Docker-managed) → `/var/opt/mssql/secrets`. **Never mount the whole
     `/var/opt/mssql` from a Windows folder**: SQL Server can't create its machine key there and
     crash-loops with `Failed to load LSA: 0xc0070102`.
   - env `SQLCMDPASSWORD=${MSSQL_SA_PASSWORD}`, so no `-P` appears on a command line
   - healthcheck: healthy only when **every** database is `ONLINE`:
     `sqlcmd -S localhost -U sa -C -b -Q "IF EXISTS (SELECT 1 FROM sys.databases WHERE state_desc <> 'ONLINE') THROW 50000, 'recovering', 1;"`,
     interval 10s, retries 10, start_period 30s. A plain `SELECT 1` passes while `SmartTaskDb` is still
     recovering after a restart, and the init step then fails with `Msg 904 … cannot be autostarted`.
   - `restart: unless-stopped`
2. **`sqlserver-init`**: the same image, one-shot
   - `depends_on: sqlserver: condition: service_healthy`
   - mounts `./docker/sqlserver:/scripts:ro`
   - runs `sqlcmd -S sqlserver -U sa -C -b -i /scripts/init.sql -v APP_DB_NAME=… APP_DB_USER=… APP_DB_PASSWORD=…`
     (password from `SQLCMDPASSWORD`) in a `bash -c` loop of up to 10 attempts, 3 s apart
   - why it exists: a new container only has `sa`. `MigrateAsync()` has to connect as the app login
     to run at all, so it can't create that login. The script creates the login and the empty database;
     **the schema always comes from the EF Core migrations**
   - `restart: "no"`
3. **`api`**: `build: { context: ., dockerfile: src/SmartTask.Api/Dockerfile }`, image `smarttask-api`
   - `depends_on: sqlserver-init: condition: service_completed_successfully`
   - ports: `"${API_HTTP_PORT}:8080"`, `"${API_HTTPS_PORT}:8081"`
   - env:
     - `ASPNETCORE_ENVIRONMENT=Docker`
     - `ASPNETCORE_HTTP_PORTS=8080`, `ASPNETCORE_HTTPS_PORTS=8081`
     - `ASPNETCORE_HTTPS_PORT=${API_HTTPS_PORT}`: the **host** port, so the HTTP→HTTPS redirect
       points to a URL the browser can reach
     - `ASPNETCORE_Kestrel__Certificates__Default__Path=/https/smarttask-api.pfx`,
       `ASPNETCORE_Kestrel__Certificates__Default__Password=${CERT_PASSWORD}`
     - `Database__Password=${APP_DB_PASSWORD}`, `Jwt__SigningKey=${JWT_SIGNING_KEY}`, `Ai__ApiKey=${AI_API_KEY}`
     - `Bootstrap__Admin__FirstName/LastName/Email/Password` from the `BOOTSTRAP_ADMIN_*` values: on start-up
       the API creates that Admin **only when no active admin exists** (see `/create-features`)
   - volumes: `${CERT_DIR}:/https:ro`, `./logs/api:/app/logs`
   - `restart: unless-stopped`
4. **Client slot**: a commented-out `smart-task-web` service, so the client only needs uncommenting
   later (`build: ./client/smart-task-web`, `ports: "4200:80"`, `depends_on: [api]`, same network,
   note: its API base URL is `https://localhost:${API_HTTPS_PORT}/api/v1`). CORS already allows
   `localhost:4200`.

Top-level volumes. Data and log are binds to folders under `D:\Projects\TaskPlus`; secrets is Docker-managed:
```yaml
volumes:
  sqlserver-data:
    driver: local
    driver_opts: { type: none, o: bind, device: ${SQLSERVER_DATA_DIR}/data }
  sqlserver-log:
    driver: local
    driver_opts: { type: none, o: bind, device: ${SQLSERVER_DATA_DIR}/log }
  sqlserver-secrets: {}
```
Create `data/` and `log/` first (`mkdir -p`). If SQL Server fails to start
(`docker compose logs sqlserver`), look for the first `ERROR` line before the crash dump, not the
`find: … Permission denied` noise. **Stop and ask** before moving the data files off the host folder.
If `docker volume rm` is needed after changing `driver_opts`, run `docker compose down` first.

## 5. Verify

```bash
dotnet build SmartTask.slnx
dotnet test SmartTask.slnx
dotnet ef migrations has-pending-model-changes -p src/SmartTask.Persistence -s src/SmartTask.Api --no-build   # no changes
docker compose config --quiet                       # compose file is valid
docker compose up -d --build
docker compose ps                                   # sqlserver healthy, sqlserver-init exited 0, api running
docker compose logs api | grep -iE "migration|schema is up to date"   # first run: "Applying 1 pending migration(s)"; later runs: "up to date"
#   PowerShell has no grep: docker compose logs api | Select-String -Pattern migration
curl -s  http://localhost:5180/health/live          # Healthy (or a 307 to https)
curl -sk https://localhost:7180/health/ready        # Healthy, database check included
curl -sk -o /dev/null -w "%{http_code}" https://localhost:7180/swagger/index.html   # 200
grep -rnE "Password=|SigningKey\"\s*:\s*\"[^\"]+|MSSQL_SA_PASSWORD=[^<]" --include=*.json --include=*.yml --include=*.md --include=Dockerfile .   # nothing (.env is ignored)
git status --porcelain | grep -E "\.env$|\.pfx$"    # nothing
```
Then do the persistence round trip: register a user through `https://localhost:7180/api/v1/auth/register`,
run `docker compose down` and `docker compose up -d` **twice** (the second start is the one that
exercises recovery of an existing `SmartTaskDb`), and log in with the same user. From Git Bash, set
`MSYS_NO_PATHCONV=1` before `docker compose exec … /opt/...`, or the path is rewritten to a Windows one. Then check that
`D:\Projects\TaskPlus\sqlserver-data` contains `data/SmartTaskDb.mdf`.

Then the first-admin flow: `docker compose logs api` shows `Bootstrap admin <id> created` on the first
start and `Admin bootstrap skipped: an active admin already exists` after a restart. Log in with the
`BOOTSTRAP_ADMIN_*` credentials (role `Admin`), create a ProjectManager with `POST /api/v1/users`, and log
in as that user (role `ProjectManager`).

In Swagger UI, check: the Auth endpoints have no padlock; after **Authorize** with a TeamMember token,
`GET /users` returns 403 with `INSUFFICIENT_ROLE`; schema names show as `BaseResponse<UserDto>`.

Self-check, report ✅/❌:
- [ ] Two-stage Dockerfile (SDK → `dotnet publish`; aspnet runtime, non-root user), cached restore layer
- [ ] `docker compose up -d --build` starts sqlserver (healthy), sqlserver-init (exit 0) and api
- [ ] API reachable on `http://localhost:<http>` and `https://localhost:<https>`; ports match launchSettings or the change is reported
- [ ] No pending model changes; `dotnet ef database update` succeeds locally
- [ ] App connects to `SmartTaskDb` as `Samin` on port 1433; `MigrateAsync` runs on container start (flag or `DOTNET_RUNNING_IN_CONTAINER`), never in tests or plain `dotnet run`
- [ ] Data survives `docker compose down` / `up`; files are in `D:\Projects\TaskPlus\sqlserver-data`
- [ ] Swagger UI loads over HTTPS, JWT Authorize works, anonymous endpoints have no padlock, generic schemas are named `Type<Arg>`
- [ ] No secret in any tracked file; `.env` and `*.pfx` ignored; `.env.example` has placeholders only
- [ ] `SmartTaskDbContext` has no connection configuration; Persistence still doesn't reference the Api
- [ ] Exactly one bootstrap admin after any number of restarts; it can sign in and create users with `POST /users`
- [ ] Commented client service and CORS origins ready for the Angular app
- [ ] Build passes, tests pass (report the count)

## 6. Finish

1. Update `docs/PROGRESS.md` (new row `/deploy-backend-services`, and a log entry listing the
   files created and changed, the ports used, and the open questions).
2. Add a short "Run with Docker" section to `docs/WORKFLOW.md`: the `.env` setup, the certificate
   commands, `docker compose up -d --build`, the URLs, and `docker compose down` (data kept) vs.
   deleting the data folder (data lost).
3. Print the URLs (API HTTP/HTTPS, Swagger, health, SQL Server for SSMS) and the manual steps
   (trust the dev certificate, fill in `AI_API_KEY` if wanted).
4. Suggested commit: `build(docker): containerised API with SQL Server, compose and Swagger schema ids`.
