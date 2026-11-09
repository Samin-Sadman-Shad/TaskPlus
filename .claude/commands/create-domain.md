---
description: Create domain entities, enums, constants and exceptions in SmartTask.Domain from docs/DOMAIN-MODEL.md
argument-hint: "[EntityName]  (optional — omit to generate every entity)"
allowed-tools: Read, Write, Edit, Glob, Grep, Bash(dotnet new:*), Bash(dotnet sln:*), Bash(dotnet add:*), Bash(dotnet build:*), Bash(dotnet test:*), Bash(ls:*), Bash(mkdir:*)
---

# /create-domain

Generate the **Domain layer** for the Smart Task Management System.

**Scope:** `$ARGUMENTS` — if empty, generate all common types, enums, constants, exceptions and
every entity in `docs/DOMAIN-MODEL.md`. If an entity name is given, generate/update only that
entity plus anything it depends on that is missing.

## 0. Read context first

Read these before writing any code:
1. `CLAUDE.md`
2. `docs/DOMAIN-MODEL.md` ← source of truth for fields, rules and behaviour
3. `docs/ARCHITECTURE.md` (Domain section)
4. `docs/CONVENTIONS.md`
5. Existing files under `src/SmartTask.Domain/` (if any) — update them, don't duplicate.

If the spec is ambiguous or missing something you need, **stop and ask** instead of inventing it.

## 1. Ensure the project exists

If `src/SmartTask.Domain/SmartTask.Domain.csproj` does not exist:

```bash
dotnet new sln -n SmartTask            # only if SmartTask.sln or SmartTasl.slnx is missing
dotnet new classlib -n SmartTask.Domain -o src/SmartTask.Domain -f net10.0
dotnet sln SmartTask.sln add src/SmartTask.Domain/SmartTask.Domain.csproj
```

Delete the template `Class1.cs`. If `Directory.Build.props` is missing at the repo root, create it with
`Nullable=enable`, `ImplicitUsings=enable`, `LangVersion=latest`, `TreatWarningsAsErrors=true`.

**The Domain project must have zero project references and zero NuGet packages.**

## 2. Folder layout

```
src/SmartTask.Domain/
├── Common/       BaseEntity.cs, AuditableEntity.cs, ISoftDeletable.cs
├── Constants/    UserConstraints.cs, ProjectConstraints.cs, TaskItemConstraints.cs,
│                 PagingConstraints.cs, AiConstraints.cs
├── Entities/     User.cs, RefreshToken.cs, Project.cs, ProjectMember.cs, TaskItem.cs
├── Enums/        UserRole.cs, TaskItemStatus.cs, TaskPriority.cs
└── Exceptions/   DomainException.cs, InvalidStatusTransitionException.cs
```

## 3. Rules for every entity

- Inherit from `BaseEntity` or `AuditableEntity` as the spec says; implement `ISoftDeletable` where listed.
- **All setters private** (`{ get; private set; }`). Audit and soft-delete properties use
  `private set` too — the persistence interceptor sets them via EF's change tracker.
- A **private parameterless constructor** for EF Core (`private Project() { }`), with
  `= null!` initialisers for required reference navigations/strings to satisfy nullable analysis.
- A **public static `Create(...)` factory** that validates input and returns a fully valid entity.
- **Behaviour methods** for every state change listed in the spec (`Update`, `AssignTo`,
  `ChangeStatus`, ...). No public property mutation outside these methods.
- Validate invariants with guard clauses and throw `DomainException` with a clear message. Use the
  constants from `Constants/` for lengths — no magic numbers. Trim string input.
- Collections: `private readonly List<T> _items = [];` exposed as `public IReadOnlyCollection<T> Items => _items.AsReadOnly();`
- Time is passed in as a parameter (`DateTimeOffset now`) — never call `DateTime.UtcNow` in the domain.
- Ids: `BaseEntity` sets `Id = Guid.CreateVersion7()` in its constructor; `protected set`.
- No EF Core attributes, no `System.ComponentModel.DataAnnotations`, no serialization attributes.
- Enums with explicit integer values exactly as in the spec.
- XML doc comments on each entity and on non-trivial behaviour methods.

## 4. Specific behaviour to implement

- **`TaskItem.ChangeStatus`** enforces the transition table in `docs/DOMAIN-MODEL.md`; on
  `Completed` set `CompletedAt = now`; when leaving `Completed` clear it. Invalid transitions throw
  `InvalidStatusTransitionException(from, to)`. Keep the table as a single private static
  `IReadOnlyDictionary<TaskItemStatus, TaskItemStatus[]>` so it is easy to read and test.
- **`TaskItem.IsOverdue(DateOnly today)`** per spec.
- **`Project.Create/Update`** reject `EndDate < StartDate`.
- **`Project.RemoveMember`** must not remove the owner.
- **`User.Create`** normalises email (`Trim()`, `NormalizedEmail = ToUpperInvariant()`), default role `TeamMember`.
- **`User.ChangePasswordHash`** revokes all active refresh tokens.
- **`RefreshToken`** stores only `TokenHash`; `IsActive(now)` = not revoked and not expired.

## 5. Unit tests

If `tests/SmartTask.Domain.UnitTests` does not exist, create an xUnit project (net10.0), reference
the Domain project and add it to the solution. Add focused tests for:
- every allowed and a sample of disallowed `TaskItem` status transitions (use `[Theory]`),
- `CompletedAt` set/cleared,
- `Project` date rule and owner-removal guard,
- `User.Create` email normalisation and default role,
- `RefreshToken.IsActive`.

## 6. Verify

```bash
dotnet build SmartTask.sln
dotnet test tests/SmartTask.Domain.UnitTests
```

Fix all errors and warnings before finishing.

Self-check, and report each item as ✅/❌:
- [ ] Domain project has no project references and no packages
- [ ] No public setters on any entity
- [ ] Every entity has a private EF constructor and a `Create` factory
- [ ] No magic numbers; constants used
- [ ] No `DateTime.Now` / `UtcNow` anywhere in Domain
- [ ] Fields match `docs/DOMAIN-MODEL.md` exactly (list any deviation)

## 7. Finish

1. Update `docs/PROGRESS.md` (status table + a log entry with created/updated files, build/test result,
   open questions).
2. Print a short summary: files created/updated, tests passed, and a suggested commit message, e.g.
   `feat(domain): add entities, enums and invariants`.
3. Do **not** create repositories, DbContext, DTOs or controllers in this command.
