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
