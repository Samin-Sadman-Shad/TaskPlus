# Agentic Development Workflow (Claude Code)

## The loop for every step

1. **Plan** — start Claude Code in the repo root. For bigger steps press `Shift+Tab` into plan mode
   first and ask Claude to describe what the command will generate. Approve or correct the plan.
2. **Run the command** — e.g. `/create-domain` or `/create-domain TaskItem`.
3. **Verify** — the command runs `dotnet build` (and `dotnet test` where tests exist) and prints a
   checklist. Read the diff yourself: `git diff --stat`, then the files.
4. **Review** — ask Claude for a self-review against `CLAUDE.md` §6, or run `/review`.
5. **Commit** — one conventional commit per command run.
6. **Log** — the command updates `docs/PROGRESS.md`; check it reflects reality.

Use `/clear` between unrelated steps so each command starts with a clean context; `CLAUDE.md`
and the docs give it everything it needs.

## Order

```
/create-domain            → SmartTask.Domain
/create-repositories      → Application/Contracts/Persistence + SmartTask.Persistence
/create-dto               → Application/DTOs
/implement-validators     → Application/Validators (+ tests)
```

### Suggested next commands (not yet written)

Following the assignment's recommended order, the next commands to add in `.claude/commands/` are:

| Command                 | Scope                                                              |
|-------------------------|--------------------------------------------------------------------|
| `/implement-auth`       | JWT, refresh-token rotation, password hashing, register/login/logout |
| `/create-features`      | CQRS commands/queries + handlers per entity, permission checks      |
| `/create-api`           | Controllers, `ApiResponse<T>`, exception handler, Swagger, rate limiting, health checks |
| `/implement-ai`         | `ITaskDescriptionImprover` with GitHub Models + `PROMPTS.md`       |
| `/create-angular-feature` | Lazy-loaded feature with guards, interceptors, reactive forms     |
| `/write-docs`           | README.md, Postman/Swagger export                                  |

## Changing the model later

1. Edit `docs/DOMAIN-MODEL.md`.
2. Re-run the commands with the entity name: `/create-domain Project`, `/create-repositories Project`,
   `/create-dto Project`, `/implement-validators Project`.
3. Add a migration.

Commands are written to be **idempotent**: they update existing files rather than duplicating them.
