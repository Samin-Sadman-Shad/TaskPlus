# Progress Log

Updated by each Claude Code command when it finishes. Newest entry first.

| Step | Command                 | Status      | Scope | Notes |
|------|-------------------------|-------------|-------|-------|
| 1    | `/create-domain`        | Done (tests not run) | all   | Build ✅; test run blocked by Windows Application Control policy |
| 2    | `/create-repositories`  | Not started |       |       |
| 3    | `/create-dto`           | Not started |       |       |
| 4    | `/implement-validators` | Not started |       |       |

## Log

<!-- Commands append entries below in this format:
### YYYY-MM-DD — /command-name [scope]
- Created: …
- Updated: …
- Build: ✅ / Tests: ✅ (n passed)
- Open questions / follow-ups: …
-->

### 2026-11-10 — /create-domain (all)
- Solution root is `TaskPlus/` (existing `TaskPlus/SmartTask.slnx`); paths below are relative to it.
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
  - `TaskPlus/src/TaskPlus.csproj` (template Web API, commented out of the solution) sits in a folder that is a
    parent of `src/SmartTask.Domain`. If it is ever built, its default globbing will pick up the Domain sources.
    Remove it or move it before adding `SmartTask.Api`.
