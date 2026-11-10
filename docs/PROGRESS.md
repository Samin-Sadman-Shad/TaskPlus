# Progress Log

Updated by each Claude Code command when it finishes. Newest entry first.

| Step | Command                 | Status      | Scope | Notes |
|------|-------------------------|-------------|-------|-------|
| 1    | `/create-domain`        | Done        | all   | Build ✅; Tests ✅ (53 passed) |
| 2    | `/create-repositories`  | Done        | all   | Build ✅; Tests ✅ (53); LocalDB smoke test ✅ (40 checks); no migration yet |
| 3    | `/create-dto`           | Done        | all   | Build ✅; Tests ✅ (53); sensitive-field grep empty; scratch mapping check ✅ (10) |
| 4    | `/implement-validators` | Not started |       |       |

## Log

<!-- Commands append entries below in this format:
### YYYY-MM-DD — /command-name [scope]
- Created: …
- Updated: …
- Build: ✅ / Tests: ✅ (n passed)
- Open questions / follow-ups: …
-->

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
