# CLAUDE.md — Smart Task Management System

This file is loaded automatically by Claude Code. It is the **single source of project context**.
Read it fully before doing any work, and follow the linked docs for details.

## 1. What we are building

A production-ready **Smart Task Management System**: users manage **Projects**, each project
contains **Tasks**, tasks are assigned to users, and a dashboard shows statistics. An AI endpoint
improves task descriptions. The assignment is graded on **architecture, code quality, security,
maintainability, design patterns and documentation**, not on feature count.

Full requirement: `docs/REQUIREMENTS.md`.

## 2. Tech stack

| Area      | Choice                                                                    |
|-----------|---------------------------------------------------------------------------|
| Runtime   | .NET 10 (C# latest), ASP.NET Core Web API                                 |
| Data      | EF Core 10 + SQL Server, code-first migrations                            |
| Auth      | JWT access tokens + rotating refresh tokens, role-based authorization     |
| Validation| FluentValidation                                                          |
| Logging   | Serilog (structured, console + rolling file)                              |
| API docs  | Swagger / OpenAPI                                                         |
| AI        | GitHub Models (OpenAI-compatible chat completions endpoint)               |
| Frontend  | Angular (latest stable, ≥ 18), standalone components, signals, TypeScript strict |
| Tests     | xUnit, FluentAssertions-style assertions (or Shouldly), FluentValidation.TestHelper, NSubstitute |

Do **not** add AutoMapper or MediatR (both moved to commercial licences). Use explicit mapping
extension methods and a small in-house CQRS dispatcher or plain handler classes.

## 3. Architecture (Clean Architecture)

```
<repo root>
├── SmartTask.slnx                 # solution file (lives at the repo root)
├── Directory.Build.props          # shared build settings for every project
├── src/
│   ├── SmartTask.Domain/          # Entities, enums, value rules, domain exceptions. NO dependencies.
│   ├── SmartTask.Application/     # Use cases (CQRS), DTOs, validators, contracts (interfaces).
│   │   ├── Contracts/
│   │   │   ├── Persistence/       # IRepository<T>, I<Entity>Repository, IUnitOfWork
│   │   │   ├── Identity/          # ICurrentUserService, IJwtTokenService, IPasswordHasher
│   │   │   └── Ai/                # ITaskDescriptionImprover
│   │   ├── Common/Models/         # PagedResult<T>, query parameter objects, ApiResponse<T>
│   │   ├── DTOs/<Feature>/        # Create/Update/Read DTOs + mapping extensions
│   │   ├── Validators/<Feature>/  # FluentValidation validators
│   │   └── Features/<Feature>/    # Commands, Queries, Handlers
│   ├── SmartTask.Persistence/     # DbContext, EF configurations, repositories, migrations
│   ├── SmartTask.Infrastructure/  # JWT, password hashing, AI client, time, email, etc.
│   └── SmartTask.Api/             # Controllers, middleware, DI composition root, Swagger
├── tests/
│   ├── SmartTask.Domain.UnitTests/
│   └── SmartTask.Application.UnitTests/
└── client/smart-task-web/         # Angular app
```

**Layout rule:** the solution file, `Directory.Build.props`, `src/` and `tests/` sit directly at the
repo root. Do not nest them in another folder. Every production project goes in
`src/<ProjectName>/` and every test project goes in `tests/<ProjectName>/`. Add each one to `SmartTask.slnx`
under the matching `/src/` or `/tests/` solution folder. Run all `dotnet` commands from the repo root.

**Dependency rule (never violate):**
`Domain ← Application ← Persistence / Infrastructure ← Api`
- Domain references nothing.
- Application references Domain only. It defines interfaces; it never references EF Core.
- Persistence and Infrastructure implement Application interfaces.
- Api is the composition root and the only project that references everything.

Details: `docs/ARCHITECTURE.md`. Entity spec: `docs/DOMAIN-MODEL.md`. Coding rules: `docs/CONVENTIONS.md`.

## 4. Roles & permissions (summary)

| Action                                   | Admin | ProjectManager            | TeamMember                 |
|------------------------------------------|:-----:|---------------------------|----------------------------|
| Manage users / change roles              | ✅    | ❌                         | ❌                          |
| Create a user with any role and password | ✅    | ❌                         | ❌                          |
| Create project                           | ✅    | ✅                         | ❌                          |
| Update / delete project                  | ✅    | ✅ own projects            | ❌                          |
| View / list / search projects            | ✅ all| ✅ own + member of         | ✅ member of only           |
| Create / update / delete / assign task   | ✅    | ✅ in own projects         | ❌                          |
| Update task status                       | ✅    | ✅ in own projects         | ✅ tasks assigned to them   |
| Dashboard                                | ✅ global | ✅ scoped to own projects | ✅ scoped to own tasks    |
| AI improve description                   | ✅    | ✅                         | ✅                          |
| Change own password                      | ✅    | ✅                         | ✅                          |

Self-registration always creates a **TeamMember**. Roles are only changed by an Admin, and only an
Admin can create an account with another role (`POST users`).

**First admin (bootstrap).** On start-up the API ensures one Admin exists, from the `Bootstrap:Admin`
settings (`FirstName`, `LastName`, `Email`, `Password`; environment variables or user-secrets, never
source). It creates that Admin only when **no Admin exists yet**, never overwrites a password, and never
promotes an existing account that already uses the email (that would let anyone pre-register the admin
email). With no settings it does nothing.

**Temporary passwords.** A password set by an Admin (`POST users`) is temporary: the user can sign in,
but every endpoint except `GET users/me` and `POST users/me/password` answers 403
`PASSWORD_CHANGE_REQUIRED` until they choose their own. Changing a password always needs the current
one and signs out every other session.

## 5. Agentic workflow — custom commands

Commands live in `.claude/commands/`. Run them **in this order**; each one builds on the previous.

| # | Command                       | Produces                                                         |
|---|-------------------------------|------------------------------------------------------------------|
| 1 | `/create-domain [Entity]`     | Domain entities, enums, constraints, exceptions in `SmartTask.Domain` |
| 2 | `/create-repositories [Entity]` | Interfaces in `Application/Contracts/Persistence`, implementations in `SmartTask.Persistence` |
| 3 | `/create-dto [Entity]`        | CQRS DTOs (Create / Update / Read) + mapping in `Application/DTOs` |
| 4 | `/implement-validators [Entity]` | FluentValidation validators for Create / Update DTOs + tests   |

The argument is optional: pass an entity name (e.g. `Project`) to scope the command, or omit it to
process every entity in `docs/DOMAIN-MODEL.md`.

How to work with the commands: `docs/WORKFLOW.md`. Progress is tracked in `docs/PROGRESS.md` —
**every command must update it when it finishes.**

## 6. Non-negotiable rules

1. **Spec first.** `docs/DOMAIN-MODEL.md` is the source of truth for entities, fields, lengths and
   rules. If code and spec disagree, stop and ask; do not silently invent fields.
2. **Never expose sensitive or server-controlled data** in DTOs (password hashes, refresh tokens,
   soft-delete flags, concurrency tokens, audit "by" fields, normalized columns).
3. **Async all the way** for I/O, with `CancellationToken` as the last parameter.
4. **No `IQueryable` leaves the Persistence layer.** Repositories return entities, read models or
   `PagedResult<T>`.
5. **Sorting is whitelisted.** Never build sort/filter expressions from raw client strings.
6. **Shared constants for lengths/limits** live in `SmartTask.Domain/Constants` and are used by
   both EF configurations and validators — no magic numbers.
7. **No secrets in source.** Use `dotnet user-secrets` locally and environment variables in deployment.
8. **Build must pass** (`dotnet build`) and existing tests must pass before a command reports done.
9. Keep changes scoped to the command's responsibility. Don't create controllers while running
   `/create-dto`, etc.
10. Prefer small, readable classes. Follow SOLID; apply a pattern only when it removes real duplication
    or coupling, and say why in a short XML doc comment if non-obvious.

## 7. Useful commands

```bash
dotnet build SmartTask.slnx
dotnet test SmartTask.slnx
dotnet ef migrations add <Name> -p src/SmartTask.Persistence -s src/SmartTask.Api
dotnet ef database update      -p src/SmartTask.Persistence -s src/SmartTask.Api
cd client/smart-task-web && npm start
```
