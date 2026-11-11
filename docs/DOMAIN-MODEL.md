# Domain Model — Source of Truth

Every command reads this file. Change the model **here first**, then re-run the commands.

Legend for the "DTO exposure" column:
- **R** = in Read DTO, **C** = accepted in Create DTO, **U** = accepted in Update DTO
- **—** = never exposed (sensitive or server-controlled)

---

## Common base types (`SmartTask.Domain/Common`)

| Type              | Members                                                                  |
|-------------------|---------------------------------------------------------------------------|
| `BaseEntity`      | `Guid Id` (protected set, `Guid.CreateVersion7()` by default)             |
| `AuditableEntity` : `BaseEntity` | `DateTimeOffset CreatedAt`, `Guid? CreatedBy`, `DateTimeOffset? UpdatedAt`, `Guid? UpdatedBy` — set by the persistence interceptor, never by callers |
| `ISoftDeletable`  | `bool IsDeleted`, `DateTimeOffset? DeletedAt`, `Guid? DeletedBy`          |

`CreatedAt` / `UpdatedAt` are exposed in Read DTOs only. `CreatedBy`, `UpdatedBy`, `DeletedBy`,
`IsDeleted`, `DeletedAt` are **never** exposed.

---

## Enums (`SmartTask.Domain/Enums`)

Explicit integer values; never reorder.

```csharp
public enum UserRole       { Admin = 1, ProjectManager = 2, TeamMember = 3 }
public enum TaskItemStatus { ToDo = 1, InProgress = 2, Completed = 3, Cancelled = 4 }
public enum TaskPriority   { Low = 1, Medium = 2, High = 3, Critical = 4 }
```

`TaskItemStatus` (not `TaskStatus`) avoids clashing with `System.Threading.Tasks.TaskStatus`.
Enums are serialized as **strings** in the API (`JsonStringEnumConverter`) and stored as `int`.

---

## Constants (`SmartTask.Domain/Constants`)

Used by EF configurations **and** validators.

```csharp
public static class UserConstraints
{
    public const int FirstNameMaxLength = 100;
    public const int LastNameMaxLength  = 100;
    public const int EmailMaxLength     = 256;
    public const int PasswordMinLength  = 8;
    public const int PasswordMaxLength  = 128;
}

public static class ProjectConstraints
{
    public const int NameMinLength        = 3;
    public const int NameMaxLength        = 150;
    public const int DescriptionMaxLength = 2000;
}

public static class TaskItemConstraints
{
    public const int TitleMinLength       = 3;
    public const int TitleMaxLength       = 200;
    public const int DescriptionMaxLength = 4000;
}

public static class PagingConstraints
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize     = 100;
    public const int SearchTermMaxLength = 100;
}

public static class AiConstraints
{
    public const int InputMinLength = 3;
    public const int InputMaxLength = 2000;
    public const int OutputTargetLength  = 1200; // asked for in the prompt; hard cap is TaskItemConstraints.DescriptionMaxLength
    public const int MaxSuggestions      = 3;
    public const int SuggestionMaxLength = 200;
}
```

---

## Entity: `User` : `AuditableEntity`

| Property          | Type                    | Rules                                       | DTO exposure |
|-------------------|-------------------------|---------------------------------------------|--------------|
| `Id`              | `Guid`                  |                                             | R            |
| `FirstName`       | `string`                | required, ≤ 100, trimmed                    | R C U        |
| `LastName`        | `string`                | required, ≤ 100, trimmed                    | R C U        |
| `Email`           | `string`                | required, ≤ 256, unique (case-insensitive)  | R C          |
| `NormalizedEmail` | `string`                | upper-invariant of Email, unique index      | —            |
| `PasswordHash`    | `string`                | required                                    | —            |
| `Role`            | `UserRole`              | default `TeamMember`                        | R (changed only via admin `ChangeUserRoleDto`) |
| `IsActive`        | `bool`                  | default `true`                              | R            |
| `LastLoginAt`     | `DateTimeOffset?`       |                                             | R            |
| `RefreshTokens`   | `IReadOnlyCollection<RefreshToken>` | backing field `_refreshTokens`  | —            |

Computed: `FullName => $"{FirstName} {LastName}"` (R).

Behaviour:
- `static User Create(firstName, lastName, email, passwordHash)` → role `TeamMember`.
- `UpdateProfile(firstName, lastName)`
- `ChangeRole(UserRole role)`
- `ChangePasswordHash(string newHash)` → also revokes all active refresh tokens.
- `Deactivate()` / `Activate()`
- `RecordLogin(DateTimeOffset at)`
- `AddRefreshToken(RefreshToken token)`, `RevokeAllRefreshTokens(DateTimeOffset at, string reason)`

---

## Entity: `RefreshToken` : `BaseEntity`

| Property             | Type              | Rules                                   | DTO exposure |
|----------------------|-------------------|-----------------------------------------|--------------|
| `UserId`             | `Guid`            | FK                                      | —            |
| `TokenHash`          | `string`          | SHA-256 of the raw token; raw token is never stored | — |
| `ExpiresAt`          | `DateTimeOffset`  |                                         | —            |
| `CreatedAt`          | `DateTimeOffset`  |                                         | —            |
| `CreatedByIp`        | `string?`         | ≤ 45                                    | —            |
| `RevokedAt`          | `DateTimeOffset?` |                                         | —            |
| `RevokedReason`      | `string?`         | ≤ 200                                   | —            |
| `ReplacedByTokenHash`| `string?`         | for rotation / reuse detection          | —            |

Computed: `IsExpired(DateTimeOffset now)`, `IsActive(DateTimeOffset now)`.
Behaviour: `static Create(userId, tokenHash, expiresAt, createdAt, ip)`, `Revoke(at, reason, replacedByHash?)`.

The raw refresh token is sent to the client **only** as an HttpOnly, Secure, SameSite=Strict cookie
when it is issued. It never appears in a JSON body or in the database.

---

## Entity: `Project` : `AuditableEntity`, `ISoftDeletable`

| Property      | Type                         | Rules                                     | DTO exposure |
|---------------|------------------------------|-------------------------------------------|--------------|
| `Id`          | `Guid`                       |                                           | R            |
| `Name`        | `string`                     | required, 3–150, unique per owner (non-deleted) | R C U  |
| `Description` | `string?`                    | ≤ 2000                                    | R C U        |
| `StartDate`   | `DateOnly`                   | required                                  | R C U        |
| `EndDate`     | `DateOnly?`                  | ≥ StartDate                               | R C U        |
| `OwnerId`     | `Guid`                       | set from current user, never from client  | R            |
| `Owner`       | `User`                       | navigation                                | R (as `OwnerName`) |
| `Members`     | `IReadOnlyCollection<ProjectMember>` | backing field                     | R (as count / list in details) |
| `Tasks`       | `IReadOnlyCollection<TaskItem>` | backing field                          | R (counts in summary) |
| `RowVersion`  | `byte[]`                     | concurrency token                         | —            |

Behaviour:
- `static Project Create(name, description, startDate, endDate, ownerId)` — validates `EndDate ≥ StartDate`.
- `Update(name, description, startDate, endDate)`
- `AddMember(Guid userId)` (idempotent), `RemoveMember(Guid userId)` (owner cannot be removed)
- `bool IsOwnedBy(Guid userId)`, `bool HasMember(Guid userId)`

---

## Entity: `ProjectMember` (join entity, composite key `ProjectId + UserId`)

| Property    | Type             | DTO exposure |
|-------------|------------------|--------------|
| `ProjectId` | `Guid`           | —            |
| `UserId`    | `Guid`           | R (in member list) |
| `User`      | `User`           | R (name/email) |
| `JoinedAt`  | `DateTimeOffset` | R            |

---

## Entity: `TaskItem` : `AuditableEntity`, `ISoftDeletable`

Named `TaskItem` to avoid conflict with `System.Threading.Tasks.Task`.

| Property      | Type              | Rules                                        | DTO exposure |
|---------------|-------------------|----------------------------------------------|--------------|
| `Id`          | `Guid`            |                                              | R            |
| `ProjectId`   | `Guid`            | from route on create, immutable              | R            |
| `Title`       | `string`          | required, 3–200                              | R C U        |
| `Description` | `string?`         | ≤ 4000                                       | R C U        |
| `Status`      | `TaskItemStatus`  | default `ToDo`; changed only via `ChangeStatus` | R (+ `UpdateTaskStatusDto`) |
| `Priority`    | `TaskPriority`    | default `Medium`                             | R C U        |
| `DueDate`     | `DateOnly?`       | not in the past on create                    | R C U        |
| `AssigneeId`  | `Guid?`           | must be a member of the project              | R C (+ `AssignTaskDto`) |
| `Assignee`    | `User?`           | navigation                                   | R (as `AssigneeName`) |
| `CompletedAt` | `DateTimeOffset?` | set when status becomes Completed            | R            |
| `RowVersion`  | `byte[]`          | concurrency token                            | —            |

Computed: `bool IsOverdue(DateOnly today)` → `DueDate < today && Status is ToDo or InProgress` (R as `IsOverdue`).

Behaviour:
- `static TaskItem Create(projectId, title, description, priority, dueDate, assigneeId)`
- `UpdateDetails(title, description, priority, dueDate)`
- `AssignTo(Guid? userId)` (null = unassign)
- `ChangeStatus(TaskItemStatus newStatus, DateTimeOffset now)` — enforces transitions below.

Allowed status transitions (anything else throws `InvalidStatusTransitionException`):

| From        | To                                   |
|-------------|--------------------------------------|
| ToDo        | InProgress, Completed, Cancelled     |
| InProgress  | ToDo, Completed, Cancelled           |
| Completed   | InProgress (reopen; clears `CompletedAt`) |
| Cancelled   | ToDo (reopen)                        |

---

## Non-entity DTOs (no backing entity)

| DTO                              | Purpose                                   |
|----------------------------------|-------------------------------------------|
| `RegisterRequestDto`             | FirstName, LastName, Email, Password, ConfirmPassword |
| `LoginRequestDto`                | Email, Password                           |
| `AuthResponseDto`                | AccessToken, AccessTokenExpiresAt, User (`UserDto`) — the refresh token travels in a cookie, never here |
| `ChangeUserRoleDto`              | Role (Admin only)                         |
| `ImproveDescriptionRequestDto`   | Title (optional context), Description     |
| `ImproveDescriptionResponseDto`  | OriginalDescription, ImprovedDescription, Suggestions (list) |
| `DashboardStatsDto`              | TotalProjects, TotalTasks, TasksByStatus, TasksByPriority, CompletedCount, PendingCount, OverdueCount, UpcomingDueTasks |
