---
description: Implement FluentValidation validators for the Create / Update (and action) DTOs in SmartTask.Application/Validators, with unit tests
argument-hint: "[EntityName | Auth | Ai]  (optional — omit to validate every request DTO)"
allowed-tools: Read, Write, Edit, Glob, Grep, Bash(dotnet new:*), Bash(dotnet sln:*), Bash(dotnet add:*), Bash(dotnet build:*), Bash(dotnet test:*), Bash(ls:*), Bash(mkdir:*)
---

# /implement-validators

Implement input validation for every **inbound** DTO using **FluentValidation**. Read DTOs are never
validated.

**Scope:** `$ARGUMENTS` — if empty, cover every request DTO under `src/SmartTask.Application/DTOs/`.
Otherwise only that feature folder.

## 0. Read context first

1. `CLAUDE.md`
2. `docs/DOMAIN-MODEL.md` — rules and limits per field
3. `src/SmartTask.Domain/Constants/` — use these constants, never literal numbers
4. The request DTOs under `src/SmartTask.Application/DTOs/` and the query parameter types under
   `src/SmartTask.Application/Common/Models/`
5. Existing validators under `src/SmartTask.Application/Validators/`

If the DTOs don't exist yet, stop and ask the user to run `/create-dto` first.

## 1. Packages and registration

```bash
dotnet add src/SmartTask.Application package FluentValidation
dotnet add src/SmartTask.Application package FluentValidation.DependencyInjectionExtensions
```

In `src/SmartTask.Application/DependencyInjection.cs` (create if missing):

```csharp
public static IServiceCollection AddApplication(this IServiceCollection services)
{
    services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);
    return services;
}
```

Do **not** use the deprecated `FluentValidation.AspNetCore` auto-validation package. Validators run
in the API's global `ValidationFilter` (`docs/API-CONVENTIONS.md` §4, built by `/implement-auth` or
`/create-api`), which returns `BaseResponse.ValidationFailed(errors)` with a 400. Add a small helper the
filter can reuse:

`src/SmartTask.Application/Common/Validation/ValidationResultExtensions.cs`
- `IReadOnlyDictionary<string, string[]> ToErrorDictionary(this ValidationResult result)` — groups
  errors by property name with camelCase keys (`"endDate"`, `"password"`).

## 2. Validator style

- `public sealed class <DtoName>Validator : AbstractValidator<<DtoName>>`
- Location: `src/SmartTask.Application/Validators/<Feature>/`
- Put shared rules in `Validators/Common/RuleBuilderExtensions.cs`, e.g.
  `ValidName(min, max)`, `ValidPassword()`, `NotInPast(TimeProvider)`, `ValidSortField(IReadOnlySet<string>)`.
  Reuse them — don't copy rules between validators (Update and Create often share a base
  `ProjectDetailsRules<T>` via an abstract validator or extension methods).
- Every rule gets a clear, user-facing `.WithMessage(...)`; use `{PropertyName}`, `{MaxLength}` placeholders.
- `.Cascade(CascadeMode.Stop)` per property where later rules would produce noise (e.g. `NotEmpty` before `MaximumLength`).
- Validators that need the current date take `TimeProvider` through the constructor
  (`DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime)`); never call `DateTime.UtcNow`.
- **Keep validators pure** — shape, format and range only. No repositories or database calls.
  Uniqueness (email, project name), existence (assignee, project) and permission checks belong in the
  services and are backed by unique indexes. State this in an XML comment on each validator where a
  reader might expect a DB check.
- Treat whitespace-only strings as empty (`NotEmpty()` + `Must(s => !string.IsNullOrWhiteSpace(s))`
  or a shared `NotBlank()` extension).

## 3. Rules per DTO

### Auth
| DTO / field | Rules |
|---|---|
| `RegisterRequestDto.FirstName`, `.LastName` | NotBlank, ≤ `UserConstraints.FirstNameMaxLength` / `LastNameMaxLength`, letters, spaces, `-`, `'`, `.` only |
| `RegisterRequestDto.Email` | NotBlank, `EmailAddress()`, ≤ `UserConstraints.EmailMaxLength` |
| `RegisterRequestDto.Password` | NotEmpty, length `PasswordMinLength`–`PasswordMaxLength`, ≥1 upper, ≥1 lower, ≥1 digit, ≥1 non-alphanumeric, must not contain the email local-part |
| `RegisterRequestDto.ConfirmPassword` | `Equal(x => x.Password)` with message "Passwords do not match." |
| `LoginRequestDto` | Email NotBlank + EmailAddress; Password NotEmpty + ≤ `PasswordMaxLength`. **Do not** apply complexity rules on login (don't reveal policy, don't block legacy passwords). |

(Refresh and logout have no request body — the token comes from the HttpOnly cookie — so they have no validator.)

### Users
| DTO | Rules |
|---|---|
| `UpdateUserProfileDto` | same name rules as registration |
| `ChangeUserRoleDto` | `Role` `IsInEnum()` |

### Projects
| DTO / field | Rules |
|---|---|
| `Name` | NotBlank, length `ProjectConstraints.NameMinLength`–`NameMaxLength` |
| `Description` | ≤ `DescriptionMaxLength` when not null |
| `StartDate` | NotEmpty (not `default(DateOnly)`) |
| `EndDate` | when present, `>= StartDate` — message "End date must be on or after the start date." |
| Create only | `StartDate` not more than 1 day in the past (tolerates time-zone edges) |
| `AddProjectMemberDto.UserId` | NotEmpty (`Guid.Empty` rejected) |

### Tasks
| DTO / field | Rules |
|---|---|
| `Title` | NotBlank, length `TaskItemConstraints.TitleMinLength`–`TitleMaxLength` |
| `Description` | ≤ `TaskItemConstraints.DescriptionMaxLength` when not null |
| `Priority` | `IsInEnum()` |
| `CreateTaskDto.DueDate` | when present, `>= today` (UTC) |
| `UpdateTaskDto.DueDate` | when present, a valid date — **no** past-date rule, so editing an already-overdue task's title doesn't fail |
| `CreateTaskDto.AssigneeId` | when present, not `Guid.Empty` |
| `UpdateTaskStatusDto.Status` | `IsInEnum()` (transition legality is a **domain** rule, enforced by `TaskItem.ChangeStatus`) |
| `AssignTaskDto.AssigneeId` | when present, not `Guid.Empty` |

### AI
| DTO / field | Rules |
|---|---|
| `ImproveDescriptionRequestDto.Description` | NotBlank, length `AiConstraints.InputMinLength`–`InputMaxLength` |
| `ImproveDescriptionRequestDto.Title` | ≤ `TaskItemConstraints.TitleMaxLength` when present |

### Query parameters (`Common/Models`)
Also validate `ProjectQueryParameters`, `TaskQueryParameters`, `UserQueryParameters` (via a shared
`PageQueryValidator<T>` base or extension):
- `PageNumber >= 1`
- `PageSize` between 1 and `PagingConstraints.MaxPageSize`
- `Search` ≤ `PagingConstraints.SearchTermMaxLength`
- `SortBy` when present must be in the type's `AllowedSortFields` (case-insensitive) — message lists
  the allowed values
- `SortDirection` `IsInEnum()`
- Enum filters `IsInEnum()` when present; date ranges: `From <= To` when both present

## 4. Unit tests

If `tests/SmartTask.Application.UnitTests` doesn't exist, create an xUnit project (net10.0),
reference `SmartTask.Application`, add it to the solution, and add `FluentValidation` (its
`FluentValidation.TestHelper` namespace ships in the main package) and
`Microsoft.Extensions.TimeProvider.Testing` (for `FakeTimeProvider`).

For each validator, in `tests/SmartTask.Application.UnitTests/Validators/<Feature>/`:
- one test that a fully valid DTO passes (`ShouldNotHaveAnyValidationErrors`),
- `[Theory]` tests for each rule's boundary (min-1, min, max, max+1) using the Domain constants,
- the cross-field rules (EndDate vs StartDate, ConfirmPassword, date ranges),
- `SortBy` whitelist accepts allowed fields and rejects an injection-looking value such as `"name; DROP TABLE"`,
- time-dependent rules with a fixed `FakeTimeProvider` date.

Use `TestValidate(...)` and `ShouldHaveValidationErrorFor(x => x.Prop)`.

## 5. Verify

```bash
dotnet build SmartTask.sln
dotnet test SmartTask.sln
```

Self-check, report ✅/❌:
- [ ] Every Create / Update / action request DTO and query parameter type has a validator
- [ ] Zero literal length numbers — all limits from `SmartTask.Domain.Constants`
- [ ] No repository / DbContext usage in any validator
- [ ] No direct clock calls; `TimeProvider` injected where needed
- [ ] Shared rules extracted; no copy-pasted rule blocks between Create and Update validators
- [ ] Validators registered via `AddValidatorsFromAssembly`
- [ ] All tests pass (report count)

## 6. Finish

1. Update `docs/PROGRESS.md` (status + log entry with test count).
2. Print a table: Validator → rules summary; list any rules you were unsure about.
3. Suggested commit: `feat(application): add FluentValidation validators and tests`.
4. Do **not** wire validators into controllers or create services in this command. `ValidationFilter`
   is set up by `/implement-auth` or `/create-api`.
