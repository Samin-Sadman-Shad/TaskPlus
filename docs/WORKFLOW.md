# Agentic Development Workflow (Claude Code)

This file is for **you**, the developer. Commands don't read it. They rely on `CLAUDE.md`, the
specs in `docs/` and their own prerequisite checks.

## The loop for every step

1. **Plan** — start Claude Code in the repo root. For bigger steps press `Shift+Tab` into plan mode
   first and ask Claude to describe what the command will generate. Approve or correct the plan.
2. **Run the command** — e.g. `/create-domain` or `/create-domain TaskItem`.
3. **Verify** — the command runs the build and tests and prints a ✅/❌ checklist. Read the diff yourself:
   `git diff --stat`, then the files.
4. **Review** — ask Claude for a self-review against `CLAUDE.md` §6, or run `/review`.
5. **Commit** — one conventional commit per command run.
6. **Log** — the command updates `docs/PROGRESS.md`; check it matches reality.

Use `/clear` between unrelated steps so each command starts with a clean context. `CLAUDE.md` reloads
automatically, and the commands read the docs they need.

## Recommended order

Follows the assignment's recommended development order.

```
Backend foundation
  1  /create-domain
  2  /create-repositories
  3  /create-dto
  4  /implement-validators
Authentication
  5  /implement-auth                 register · login · refresh · logout
Use cases and API
  6  /create-features                Users · Projects · Tasks · Dashboard services
  7  /create-api                     controllers + host setup; adds the initial migration
     /implement-authorization        role attributes · global authz handler · 401/403 error codes
AI
  8  /implement-ai                   + PROMPTS.md
Frontend
  9  /create-angular-feature core
     /create-angular-feature auth
     /create-angular-feature dashboard | projects | tasks | users
Deliverables
 10  /write-docs                     README · PROMPTS · SQL script · Postman
```

### What actually depends on what

| Command | Needs |
|---|---|
| `/create-domain` | nothing |
| `/create-repositories` | Domain |
| `/create-dto` | Domain |
| `/implement-validators` | DTOs |
| `/implement-auth` | Domain, repositories, Auth DTOs and validators |
| `/create-features` | repositories, DTOs (for that feature) |
| `/create-api` | services (skips controllers whose service is missing) |
| `/implement-authorization` | controllers, the JWT setup and `BaseResponse` |
| `/implement-ai` | AI DTOs and validator; API foundations are created if missing |
| `/create-angular-feature core` | API endpoints to call (it can be built earlier against the documented contract) |
| `/create-angular-feature <slice>` | `core` |
| `/write-docs` | a building solution; `postman` needs a runnable API |

Shared building blocks (`BaseResponse`, `BaseApiController`, `ValidationFilter`, `AppRoles`) are created
by whichever command needs them first, always from `docs/API-CONVENTIONS.md`, so the order between
`/implement-auth`, `/create-api` and `/implement-ai` doesn't matter.

## Changing the model or contract later

1. Edit the spec first: `docs/DOMAIN-MODEL.md` for entities and DTO fields,
   `docs/API-CONVENTIONS.md` for endpoints and responses.
2. Re-run every command covering the change, scoped to it, e.g. for a new `Project` field:
   `/create-domain Project` → `/create-repositories Project` → `/create-dto Projects` →
   `/implement-validators Projects` → `/create-features Projects` → `/create-api Projects` →
   `/create-angular-feature projects`.
3. Add a migration.

Commands are written to be **idempotent**: they update existing files rather than duplicating them.

## Run with Docker

Prerequisite: **Docker Desktop** (WSL 2 backend) running. `docker ps` must print a table, not an error.

> **Shortcut:** `./scripts/start.ps1` (or `./scripts/start.sh`) does steps 1–4 for you. It generates `.env` with
> random secrets and a first Admin (only if `.env` is missing), creates the data folders and the certificate, and
> starts everything. The manual steps below are for choosing your own values.

1. **Settings.** Copy `.env.example` to `.env` and fill in every `<generated>` value with a long random
   string (`.env` is git-ignored; never commit it). `SQLSERVER_DATA_DIR` is where the database files go.
2. **Certificate** for HTTPS in the container (once per machine; use the `CERT_PASSWORD` from `.env`):
   ```bash
   dotnet dev-certs https -ep "$USERPROFILE/.aspnet/https/smarttask-api.pfx" -p "<CERT_PASSWORD>"
   dotnet dev-certs https --trust
   ```
3. **Data folders** (once): create `data` and `log` inside `SQLSERVER_DATA_DIR`.
4. **Start:** `docker compose up -d --build`. SQL Server starts; `sqlserver-init` creates the database and the
   app login; the API applies the EF Core migrations (`MigrateAsync`) and starts; the `web` container builds the
   Angular app and serves it with nginx on http://localhost:4200 (`WEB_PORT`), forwarding `/api` to the API.
5. **First admin:** set `BOOTSTRAP_ADMIN_FIRST_NAME`, `_LAST_NAME`, `_EMAIL` and `_PASSWORD` in `.env`.
   On start-up the API creates that Admin only if no active admin exists; it never overwrites the
   password or promotes an existing account. Sign in with it, then create other users with any role
   through `POST /api/v1/users` (or promote self-registered users with `PATCH /api/v1/users/{id}/role`).
6. **Check:** `docker compose ps` (sqlserver healthy, sqlserver-init `Exited (0)`, api `Up`, web `healthy`) and
   `docker compose logs api` (PowerShell: `| Select-String migration`).

| What | URL |
|------|-----|
| Web app | http://localhost:4200 |
| Swagger UI | https://localhost:7180/swagger |
| API base | https://localhost:7180/api/v1 (http://localhost:5180 redirects to HTTPS) |
| Health | https://localhost:7180/health/live, https://localhost:7180/health/ready |
| SQL Server (SSMS) | `localhost,1433`, SQL login from `APP_DB_USER` / `APP_DB_PASSWORD` |

- `docker compose down` stops everything and **keeps** the data; `docker compose up -d` brings it back.
- To start from an empty database: `docker compose down`, then delete the `data` and `log` folders' contents.
- `docker compose down -v` also deletes the Docker-managed `sqlserver-secrets` volume; avoid it unless you are
  also wiping the data folders.

