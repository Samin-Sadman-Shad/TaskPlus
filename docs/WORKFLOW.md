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
