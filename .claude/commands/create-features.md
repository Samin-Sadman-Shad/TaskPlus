---
description: Create application services (interface + implementation) for every API operation, using repository contracts and returning BaseResponse objects
argument-hint: "[Users | Projects | Tasks | Dashboard]  (optional — omit to generate every feature service)"
allowed-tools: Read, Write, Edit, Glob, Grep, Bash(dotnet add:*), Bash(dotnet build:*), Bash(dotnet test:*), Bash(ls:*), Bash(mkdir:*)
---

# /create-features

Create the **service layer** that holds the use cases. Each feature gets:
- an **interface** in `Application/Contracts/Services/`, which the controller depends on, and
- an **implementation** in `Application/Services/<Feature>/`, which depends only on repository
  contracts and other abstractions, all received through **constructor injection**.

There is **one service method per controller action** listed in `docs/API-CONVENTIONS.md` §7.
Every method returns a `BaseResponse` or `BaseResponse<T>` built from what the persistence layer returned.

**Scope:** `$ARGUMENTS` — a feature name, or empty for all of `Users`, `Projects`, `Tasks`, `Dashboard`.
(`Auth` is handled by `/implement-auth`; `Ai` by `/implement-ai`.)

## 0. Read context first

1. `CLAUDE.md` — especially the roles & permissions table
2. `docs/API-CONVENTIONS.md` — response contract (§2), status rules, endpoint catalogue (§7)
3. `docs/DOMAIN-MODEL.md` — entity behaviour methods to call
4. Existing code: repository contracts, DTOs and mappings, `ICurrentUserService`, `BaseResponse`

If `BaseResponse` doesn't exist, create it exactly as in API-CONVENTIONS §2. If the repositories or
DTOs for the feature are missing, stop and say which command to run.

## 1. Folder layout

```
SmartTask.Application/
├── Contracts/Services/
│   ├── IUserService.cs
│   ├── IProjectService.cs
│   ├── ITaskService.cs
│   └── IDashboardService.cs
└── Services/
    ├── Authorization/
    │   ├── IProjectAccessGuard.cs
    │   └── ProjectAccessGuard.cs
    ├── Users/UserService.cs
    ├── Projects/ProjectService.cs
    ├── Tasks/TaskService.cs
    └── Dashboard/DashboardService.cs
```

## 2. Service interfaces (one method per endpoint)

```csharp
public interface IUserService
{
    Task<BaseResponse<UserDto>>                       GetCurrentAsync(CancellationToken ct = default);
    Task<BaseResponse<UserDto>>                       UpdateCurrentProfileAsync(UpdateUserProfileDto dto, CancellationToken ct = default);
    Task<BaseResponse<PagedResult<UserDto>>>          GetPagedAsync(UserQueryParameters parameters, CancellationToken ct = default);
    Task<BaseResponse<IReadOnlyList<UserSummaryDto>>> LookupAsync(string? search, CancellationToken ct = default);
    Task<BaseResponse<UserDto>>                       GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<BaseResponse<UserDto>>                       ChangeRoleAsync(Guid id, ChangeUserRoleDto dto, CancellationToken ct = default);
    Task<BaseResponse>                                ActivateAsync(Guid id, CancellationToken ct = default);
    Task<BaseResponse>                                DeactivateAsync(Guid id, CancellationToken ct = default);
}

public interface IProjectService
{
    Task<BaseResponse<PagedResult<ProjectSummaryDto>>> GetPagedAsync(ProjectQueryParameters parameters, CancellationToken ct = default);
    Task<BaseResponse<ProjectDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<BaseResponse<ProjectDto>> CreateAsync(CreateProjectDto dto, CancellationToken ct = default);
    Task<BaseResponse<ProjectDto>> UpdateAsync(Guid id, UpdateProjectDto dto, CancellationToken ct = default);
    Task<BaseResponse>             DeleteAsync(Guid id, CancellationToken ct = default);
    Task<BaseResponse>             AddMemberAsync(Guid projectId, AddProjectMemberDto dto, CancellationToken ct = default);
    Task<BaseResponse>             RemoveMemberAsync(Guid projectId, Guid userId, CancellationToken ct = default);
}

public interface ITaskService
{
    Task<BaseResponse<PagedResult<TaskSummaryDto>>> GetPagedAsync(TaskQueryParameters parameters, CancellationToken ct = default);
    Task<BaseResponse<PagedResult<TaskSummaryDto>>> GetPagedByProjectAsync(Guid projectId, TaskQueryParameters parameters, CancellationToken ct = default);
    Task<BaseResponse<TaskDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<BaseResponse<TaskDto>> CreateAsync(Guid projectId, CreateTaskDto dto, CancellationToken ct = default);
    Task<BaseResponse<TaskDto>> UpdateAsync(Guid id, UpdateTaskDto dto, CancellationToken ct = default);
    Task<BaseResponse>          DeleteAsync(Guid id, CancellationToken ct = default);
    Task<BaseResponse<TaskDto>> UpdateStatusAsync(Guid id, UpdateTaskStatusDto dto, CancellationToken ct = default);
    Task<BaseResponse<TaskDto>> AssignAsync(Guid id, AssignTaskDto dto, CancellationToken ct = default);
}

public interface IDashboardService
{
    Task<BaseResponse<DashboardStatsDto>> GetStatsAsync(int upcomingDays, CancellationToken ct = default);
}
```

(Write the parameter out in full as `CancellationToken cancellationToken = default` in the real code.)
XML-doc each method with its endpoint and the status codes it can return.

## 3. Resource authorization — `IProjectAccessGuard`

Keep permission rules in one place instead of scattering role checks across the services.

```csharp
public interface IProjectAccessGuard
{
    bool IsAdmin { get; }
    Task<bool> CanViewProjectAsync(Guid projectId, CancellationToken ct = default);   // admin, owner or member
    bool CanManageProject(Project project);                                           // admin or owner (PM)
    bool CanChangeTaskStatus(Project project, TaskItem task);                         // manager, or the assignee
    Guid? VisibilityScopeUserId { get; }   // null for Admin (see all); otherwise the current user id
}
```

It's implemented with `ICurrentUserService` and `IProjectRepository`. Rules come from `CLAUDE.md` §4.
**Not visible → 404, visible but not allowed → 403.**

## 4. Implementation rules

- `public sealed class ProjectService(IProjectRepository projects, IUserRepository users, IUnitOfWork unitOfWork, IProjectAccessGuard access, ICurrentUserService currentUser, TimeProvider time, ILogger<ProjectService> logger) : IProjectService`
  — primary-constructor injection of **abstractions only**. Never inject `SmartTaskDbContext`, never
  `new` up dependencies, and never use `IServiceProvider` lookups.
- Typical method flow:
  1. Load what you need through the repository.
  2. Missing or not visible → `NotFound`.
  3. Permission check through the guard → `Forbidden`.
  4. Business checks that need data (duplicate name → `Conflict`, assignee must be a project member →
     `BadRequest`).
  5. Call **domain behaviour** (`Project.Create`, `project.Update`, `task.ChangeStatus`, `task.AssignTo`).
     Never set properties directly.
  6. `await unitOfWork.SaveChangesAsync(ct)` — **once** per operation.
  7. Map to a read DTO with the mapping extensions → `Ok` / `Created` / `NoContent`.
- Catch **`DomainException`** → `BadRequest(ex.Message)` and **`ConflictException`** (concurrency or
  unique index from the UoW) → `Conflict`. Don't catch `Exception` — unexpected errors go to the
  global exception handler.
- Validation is **not** repeated here. `ValidationFilter` runs the FluentValidation validators before
  the action. Services enforce **business** rules only.
- **Owner on create** is `currentUser.UserId`. The creator is also added as a project member.
- **Lists** pass `access.VisibilityScopeUserId` to the repository, so scoping happens in SQL.
- **Delete** calls `repository.Remove(entity)`; the interceptor turns it into a soft delete. Deleting
  a project soft-deletes its tasks in the same save.
- **TaskService.UpdateStatusAsync**: a TeamMember may change status only on tasks assigned to them;
  transition legality comes from `TaskItem.ChangeStatus` (domain → 400 via `DomainException`).
- **TaskService.CreateAsync / AssignAsync**: the assignee must be an active member of the project,
  otherwise 400 "Assignee must be a member of the project."
- **UserService.ChangeRoleAsync**: an admin can't demote themselves (prevents losing the last admin)
  → 400. **DeactivateAsync** also calls `RevokeAllRefreshTokens`.
- **UserService.LookupAsync**: max 20 results, only active users, returns `UserSummaryDto`.
- **DashboardService.GetStatsAsync**: clamp `upcomingDays` to 1–30. Use `IDashboardReadRepository`
  with the guard's scope (Admin = global, PM = own/member projects, TM = tasks assigned to them).
  Fill **every** enum key in the status and priority dictionaries (zero if empty).
- "Today" = `DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime)` — never call `DateTime` directly.
- Messages are short, user-facing and consistent ("Project not found.", "You do not have permission to modify this project.").
- Log state changes at `Information` with ids (`ProjectId`, `TaskId`, `UserId`), and permission denials
  at `Warning`. No personal data beyond ids.
- Keep each method short. If one grows past ~30 lines, extract a private helper.

### Registration
Add to `AddApplication()`:
```csharp
services.AddScoped<IProjectAccessGuard, ProjectAccessGuard>();
services.AddScoped<IUserService, UserService>();
services.AddScoped<IProjectService, ProjectService>();
services.AddScoped<ITaskService, TaskService>();
services.AddScoped<IDashboardService, DashboardService>();
```
Add `Microsoft.Extensions.Logging.Abstractions` to Application if it isn't referenced yet.

## 5. Tests — `tests/SmartTask.Application.UnitTests/Services/<Feature>/`

NSubstitute for every contract, `FakeTimeProvider` for time. For each service method, cover at least:
- happy path (correct status, DTO mapped, `SaveChangesAsync` called **once**)
- not found / not visible → 404 (and `SaveChangesAsync` **not** called)
- forbidden → 403
- the feature-specific rules: duplicate project name → 409, assignee not a member → 400,
  TeamMember changing status on someone else's task → 403, invalid transition → 400,
  admin self-demotion → 400, dashboard dictionaries contain every enum key.

Use small builder helpers (`TestData.Project(...)`) instead of repeating setup code.

## 6. Verify

```bash
dotnet build SmartTask.sln
dotnet test SmartTask.sln
grep -rn "SmartTaskDbContext\|Microsoft.EntityFrameworkCore" src/SmartTask.Application   # must print nothing
```

Self-check, report ✅/❌:
- [ ] One service method for every endpoint in API-CONVENTIONS §7 within scope
- [ ] Controllers will only need `I<Feature>Service` (no repository is needed by the API)
- [ ] Constructor injection of abstractions only
- [ ] All methods return `BaseResponse`/`BaseResponse<T>` with the statuses from API-CONVENTIONS §2
- [ ] One `SaveChangesAsync` per write operation
- [ ] Permission rules live in `ProjectAccessGuard` and match `CLAUDE.md` §4
- [ ] State changes go through domain methods
- [ ] Tests pass (report count)

## 7. Finish

1. Update `docs/PROGRESS.md`.
2. Print a table: service method → endpoint → possible status codes.
3. Suggested commit: `feat(application): add feature services with resource authorization`.
4. Don't create controllers in this command — that's `/create-api`.
