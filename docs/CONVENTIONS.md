# Coding Conventions

## C#

- File-scoped namespaces; namespace = folder path (`SmartTask.Application.DTOs.Projects`).
- `Nullable` and `ImplicitUsings` enabled; `TreatWarningsAsErrors` in `Directory.Build.props`.
- One public type per file; file name = type name.
- `sealed` by default for classes that are not designed for inheritance (DTO records, validators,
  repositories, handlers).
- DTOs are `sealed record`s with `init`/positional properties. No behaviour in DTOs.
- Entities: private setters, private parameterless constructor for EF, public factory `Create`.
- Collections on entities exposed as `IReadOnlyCollection<T>` over a private `List<T>` field.
- Async methods end with `Async` and take `CancellationToken cancellationToken = default` last.
- Guard clauses over nested `if`s. Throw `DomainException` for broken invariants in the domain.
- No `DateTime.UtcNow` / `DateTimeOffset.UtcNow` — inject `TimeProvider`.
- No magic numbers — use `SmartTask.Domain.Constants`.
- XML doc comments on public interfaces and non-obvious public members.

## Naming

| Kind                  | Pattern                          | Example                         |
|-----------------------|----------------------------------|---------------------------------|
| Repository contract   | `I<Entity>Repository`            | `IProjectRepository`            |
| Repository impl       | `<Entity>Repository`             | `ProjectRepository`             |
| Create DTO            | `Create<Entity>Dto`              | `CreateProjectDto`              |
| Update DTO            | `Update<Entity>Dto`              | `UpdateProjectDto`              |
| Read DTO (details)    | `<Entity>Dto`                    | `ProjectDto`                    |
| Read DTO (list item)  | `<Entity>SummaryDto`             | `ProjectSummaryDto`             |
| Action DTO            | `<Verb><Entity><Thing>Dto`       | `UpdateTaskStatusDto`, `AssignTaskDto` |
| Mapping               | `<Entity>Mappings` (static)      | `ProjectMappings.ToDto()`       |
| Validator             | `<DtoName>Validator`             | `CreateProjectDtoValidator`     |
| EF configuration      | `<Entity>Configuration`          | `ProjectConfiguration`          |
| Command / Query       | `<Verb><Entity>Command/Query`    | `CreateProjectCommand`          |

## API

- Routes: `api/v1/projects`, `api/v1/projects/{projectId}/tasks`, `api/v1/tasks/{id}`.
- `POST` create → `201 Created` with `Location`; `PUT` full update → `200`; `PATCH` for status/assign;
  `DELETE` → `204`.
- Resource ids come from the **route**, never from the body.
- All responses wrapped in `ApiResponse<T>` (`success`, `data`, `message`, `errors`, `traceId`).
- Query string for lists: `pageNumber`, `pageSize`, `search`, `sortBy`, `sortDirection`, plus filters.

## Tests

- `MethodName_StateUnderTest_ExpectedBehavior`.
- Domain behaviour and validators are unit-tested; repositories get integration tests later
  (Testcontainers SQL Server) if time allows.

## Git

- Conventional commits: `feat(domain): add TaskItem status transitions`.
- One command run = one commit.
