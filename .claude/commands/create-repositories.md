---
description: Create repository contracts in Application/Contracts/Persistence and EF Core implementations in SmartTask.Persistence
argument-hint: "[EntityName]  (optional — omit to generate repositories for every aggregate)"
allowed-tools: Read, Write, Edit, Glob, Grep, Bash(dotnet new:*), Bash(dotnet sln:*), Bash(dotnet add:*), Bash(dotnet build:*), Bash(dotnet test:*), Bash(ls:*), Bash(mkdir:*)
---

# /create-repositories

Create the **repository contracts** (Application layer) and their **EF Core implementations**
(Persistence layer), plus the DbContext, entity configurations and Unit of Work they need.

**Scope:** `$ARGUMENTS` — if empty, process every aggregate below. If an entity name is given, create
or update only that entity's contract, implementation and EF configuration (plus shared pieces that
are missing).

## 0. Read context first

1. `CLAUDE.md`
2. `docs/DOMAIN-MODEL.md`
3. `docs/ARCHITECTURE.md` (Application + Persistence sections)
4. `docs/CONVENTIONS.md`
5. The entities in `src/SmartTask.Domain/Entities/` — the repositories must match them. If the
   Domain layer does not exist yet, stop and tell the user to run `/create-domain` first.
6. Existing files in `src/SmartTask.Application/Contracts/Persistence/` and `src/SmartTask.Persistence/`.

## 1. Ensure the projects exist

If missing, create and wire them:

```bash
dotnet new classlib -n SmartTask.Application -o src/SmartTask.Application -f net10.0
dotnet new classlib -n SmartTask.Persistence -o src/SmartTask.Persistence -f net10.0
dotnet sln SmartTask.slnx add src/SmartTask.Application src/SmartTask.Persistence
dotnet add src/SmartTask.Application reference src/SmartTask.Domain
dotnet add src/SmartTask.Persistence reference src/SmartTask.Application
dotnet add src/SmartTask.Persistence package Microsoft.EntityFrameworkCore.SqlServer
dotnet add src/SmartTask.Persistence package Microsoft.EntityFrameworkCore.Design
```

**The Application project must not reference EF Core.** Only `Microsoft.Extensions.*.Abstractions`
packages are allowed there.

## 2. Aggregates and repositories

`Project` and `TaskItem` are separate aggregates (tasks are paged/searched independently).
`ProjectMember` is accessed through `Project`. `RefreshToken` gets its own small repository because
token lookup by hash is a hot path.

| Contract                    | Implementation            |
|-----------------------------|---------------------------|
| `IRepository<TEntity>`      | `Repository<TEntity>`     |
| `IUserRepository`           | `UserRepository`          |
| `IRefreshTokenRepository`   | `RefreshTokenRepository`  |
| `IProjectRepository`        | `ProjectRepository`       |
| `ITaskItemRepository`       | `TaskItemRepository`      |
| `IDashboardReadRepository`  | `DashboardReadRepository` |
| `IUnitOfWork`               | `UnitOfWork`              |

## 3. Application layer — contracts

Location: `src/SmartTask.Application/Contracts/Persistence/`

### `IRepository<TEntity>` (generic, `where TEntity : BaseEntity`)

```csharp
Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
Task<bool>     ExistsAsync(Guid id, CancellationToken cancellationToken = default);
Task           AddAsync(TEntity entity, CancellationToken cancellationToken = default);
void           Update(TEntity entity);
void           Remove(TEntity entity);   // soft-delete is applied by the interceptor for ISoftDeletable
```

No `SaveChanges` here — that belongs to `IUnitOfWork`. No method returns `IQueryable`.

### Specific contracts (each extends `IRepository<TEntity>`)

**`IUserRepository`**
- `GetByEmailAsync(string email, ct)` — compares on `NormalizedEmail`
- `EmailExistsAsync(string email, ct)`
- `GetWithActiveRefreshTokensAsync(Guid userId, ct)`
- `GetPagedAsync(UserQueryParameters parameters, ct)` → `PagedResult<User>` (Admin user list)

**`IRefreshTokenRepository`** (extends `IRepository<RefreshToken>`)
- `GetByTokenHashAsync(string tokenHash, ct)` — includes `User`
- `RemoveExpiredAsync(DateTimeOffset olderThan, ct)` → `int` (housekeeping)

**`IProjectRepository`**
- `GetDetailsAsync(Guid id, ct)` — includes `Owner`, `Members.User`
- `GetPagedAsync(ProjectQueryParameters parameters, Guid? visibleToUserId, ct)` → `PagedResult<ProjectListItem>`
  — `visibleToUserId == null` means Admin (see all); otherwise owner **or** member.
- `NameExistsForOwnerAsync(Guid ownerId, string name, Guid? excludeProjectId, ct)`
- `IsUserOwnerOrMemberAsync(Guid projectId, Guid userId, ct)`

**`ITaskItemRepository`**
- `GetDetailsAsync(Guid id, ct)` — includes `Assignee`, `Project`
- `GetPagedAsync(TaskQueryParameters parameters, Guid? visibleToUserId, ct)` → `PagedResult<TaskItem>`
  — supports optional `ProjectId` filter.
- `GetUpcomingDueAsync(DateOnly from, DateOnly to, Guid? visibleToUserId, int take, ct)` → `IReadOnlyList<TaskItem>`

**`IDashboardReadRepository`** (read-only, does **not** extend `IRepository<T>`)
- `GetStatsAsync(Guid? scopeUserId, UserRole role, DateOnly today, ct)` → `DashboardStatsReadModel`
  — computes counts in SQL (`GroupBy` + `CountAsync`), never by loading all tasks into memory.

**`IUnitOfWork`**
- `Task<int> SaveChangesAsync(CancellationToken ct = default)`
- `Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)`
  — wraps the action in the execution strategy + transaction (needed with SQL Server retry policy).

### Supporting models

Location: `src/SmartTask.Application/Common/Models/`

- `PagedResult<T>` — `IReadOnlyList<T> Items`, `int PageNumber`, `int PageSize`, `int TotalCount`,
  computed `TotalPages`, `HasPreviousPage`, `HasNextPage`. Include a `Map<TOut>(Func<T,TOut>)` helper.
- `PageQuery` (abstract record) — `PageNumber = 1`, `PageSize = PagingConstraints.DefaultPageSize`,
  `Search`, `SortBy`, `SortDirection` (`enum SortDirection { Asc, Desc }`).
- `ProjectQueryParameters : PageQuery` — filters: `OwnerId?`, `StartDateFrom?`, `StartDateTo?`.
  Allowed `SortBy`: `name`, `startDate`, `endDate`, `createdAt` (default `createdAt desc`).
- `TaskQueryParameters : PageQuery` — filters: `ProjectId?`, `Status?`, `Priority?`, `AssigneeId?`,
  `DueDateFrom?`, `DueDateTo?`, `IsOverdue?`. Allowed `SortBy`: `title`, `dueDate`, `priority`,
  `status`, `createdAt` (default `createdAt desc`).
- `UserQueryParameters : PageQuery` — filters: `Role?`, `IsActive?`. Allowed `SortBy`: `name`, `email`, `createdAt`.
- Expose the allowed sort keys as `public static readonly IReadOnlySet<string> AllowedSortFields`
  on each parameters type (case-insensitive) so validators can reuse them.
- Read models for projections: `ProjectListItem` (project fields + `OwnerName`, `TaskCount`,
  `CompletedTaskCount`, `MemberCount`), `DashboardStatsReadModel`, `UpcomingTaskReadModel`.

## 4. Persistence layer — implementations

Location: `src/SmartTask.Persistence/`

```
SmartTask.Persistence/
├── Configurations/   UserConfiguration, RefreshTokenConfiguration, ProjectConfiguration,
│                     ProjectMemberConfiguration, TaskItemConfiguration
├── Interceptors/     AuditableEntityInterceptor, SoftDeleteInterceptor
├── Extensions/       QueryableExtensions
├── Repositories/     Repository<T> + one file per specific repository
├── SmartTaskDbContext.cs
├── UnitOfWork.cs
└── DependencyInjection.cs
```

### DbContext
- `DbSet`s for all entities; `ApplyConfigurationsFromAssembly`.
- Global query filter `e => !e.IsDeleted` for every `ISoftDeletable` entity.
- `ConfigureConventions`: `DateOnly` columns as `date`; enums stored as `int`.

### Configurations (Fluent API only, no attributes)
- Max lengths from `SmartTask.Domain.Constants`.
- `User`: unique index on `NormalizedEmail`; ignore `FullName`; `RefreshTokens` via backing field
  `_refreshTokens` (`UsePropertyAccessMode(PropertyAccessMode.Field)`).
- `RefreshToken`: unique index on `TokenHash`; cascade delete with user.
- `Project`: filtered unique index on `(OwnerId, Name)` where `IsDeleted = 0`; `RowVersion` as
  `IsRowVersion()`; `Owner` FK with `DeleteBehavior.Restrict`; backing fields for `Members` and `Tasks`.
- `ProjectMember`: composite key; `Restrict` on user FK.
- `TaskItem`: indexes on `(ProjectId, Status)`, `AssigneeId`, `DueDate`; `RowVersion`; `Assignee`
  FK with `SetNull`; ignore computed members.

### Interceptors (`SaveChangesInterceptor`)
- `AuditableEntityInterceptor` — sets `CreatedAt/CreatedBy` on Added and `UpdatedAt/UpdatedBy` on
  Modified using `TimeProvider` and `ICurrentUserService` (define `ICurrentUserService` in
  `Application/Contracts/Identity` if it doesn't exist: `Guid? UserId`, `UserRole? Role`, `bool IsAuthenticated`).
- `SoftDeleteInterceptor` — converts `Deleted` state on `ISoftDeletable` to `Modified` with
  `IsDeleted = true`, `DeletedAt`, `DeletedBy`.
- Properties are private-set on the entity, so set them via `entry.Property(nameof(...)).CurrentValue`.

### Repositories
- `Repository<T>` holds `protected readonly SmartTaskDbContext Context` and `DbSet<T> Set`.
- Read-only queries use `AsNoTracking()`; methods that return entities intended for modification
  stay tracked. Document which is which in XML comments.
- Use `Select` projections for list/read models — don't load full graphs to count things.
- `Include` only what the method's contract promises.
- Search: `EF.Functions.Like(x.Name, $"%{term}%")` on the trimmed term (parameterised — never string-concatenated SQL).

### `QueryableExtensions`
- `ToPagedResultAsync<T>(this IQueryable<T> q, int pageNumber, int pageSize, ct)` — clamps
  `pageSize` to `PagingConstraints.MaxPageSize`, runs `CountAsync` then `Skip/Take`.
- `ApplySorting<T>(this IQueryable<T> q, string? sortBy, SortDirection dir, IReadOnlyDictionary<string, Expression<Func<T, object>>> map, string defaultKey)`
  — **whitelist only**: unknown keys fall back to the default. Each repository owns its own static
  sort map. Always add a stable tiebreaker (`ThenBy(x => x.Id)`) so paging is deterministic.

### `UnitOfWork`
Wraps `SmartTaskDbContext.SaveChangesAsync`. Translate `DbUpdateConcurrencyException` into the
Application's `ConflictException` (create it under `Application/Common/Exceptions` if missing).

### `DependencyInjection.AddPersistence(this IServiceCollection, IConfiguration)`
- Register the DbContext with `UseSqlServer(connectionString, o => o.EnableRetryOnFailure())`,
  reading `ConnectionStrings:DefaultConnection`; throw a clear `InvalidOperationException` if missing.
- Add the interceptors via `AddInterceptors(sp.GetRequiredService<...>())`.
- Register all repositories and `IUnitOfWork` as **scoped**.
- Register `TimeProvider.System` as singleton if not already registered (`TryAddSingleton`).

## 5. Verify

```bash
dotnet build SmartTask.slnx
dotnet test SmartTask.slnx
```

Do **not** add a migration in this command unless the user asks; mention it as the next step.

Self-check, report ✅/❌:
- [ ] Application project has no EF Core reference
- [ ] No `IQueryable` in any contract signature
- [ ] Every async method takes a `CancellationToken`
- [ ] Sorting goes through a whitelist map; search uses parameterised `LIKE`
- [ ] Soft-delete filter and interceptors registered
- [ ] Lengths come from Domain constants
- [ ] Dashboard counts are computed in the database

## 6. Finish

1. Update `docs/PROGRESS.md` (status + log entry).
2. Print a summary of created/updated files, any assumptions, the build/test result, a suggested
   commit message (`feat(persistence): add repositories, DbContext and configurations`) and the next
   step: `dotnet ef migrations add InitialCreate -p src/SmartTask.Persistence -s src/SmartTask.Api`
   (once the Api project exists).
3. Do **not** create DTOs, handlers or controllers in this command.
