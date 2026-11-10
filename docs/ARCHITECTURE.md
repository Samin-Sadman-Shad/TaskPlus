# Architecture

## Style

**Clean Architecture** with a **service layer** in Application and **CQRS-style DTOs** (separate
write and read shapes). The domain is the centre; everything else depends inward. The response
contract and endpoint catalogue are in `docs/API-CONVENTIONS.md`.

```
            ┌──────────────────────────────┐
            │          SmartTask.Api       │  Controllers, middleware, DI root
            └──────────────┬───────────────┘
        ┌──────────────────┴──────────────────┐
┌───────▼────────────┐              ┌──────────▼───────────┐
│ SmartTask.Persistence│            │ SmartTask.Infrastructure │
│ EF Core, repos, UoW │             │ JWT, hashing, AI, time  │
└───────┬────────────┘              └──────────┬───────────┘
        └──────────────────┬──────────────────┘
            ┌──────────────▼───────────────┐
            │     SmartTask.Application    │  Use cases, DTOs, validators, contracts
            └──────────────┬───────────────┘
            ┌──────────────▼───────────────┐
            │       SmartTask.Domain       │  Entities, enums, rules
            └──────────────────────────────┘
```

## Layer responsibilities

### SmartTask.Domain
- Entities with **private setters**, factory methods / constructors that enforce invariants, and
  behaviour methods (`ChangeStatus`, `AssignTo`, `Rename`, ...).
- Enums, domain exceptions (`DomainException`), shared constraint constants.
- No EF attributes, no data annotations, no references to other projects or NuGet packages.

```
SmartTask.Domain/
├── Common/        BaseEntity, AuditableEntity, ISoftDeletable
├── Constants/     UserConstraints, ProjectConstraints, TaskItemConstraints
├── Entities/      User, RefreshToken, Project, ProjectMember, TaskItem
├── Enums/         UserRole, TaskItemStatus, TaskPriority
└── Exceptions/    DomainException, InvalidStatusTransitionException
```

### SmartTask.Application
- **Contracts** (ports) that outer layers implement.
- **Services** (`I<Feature>Service` in `Contracts/Services`, implementations in `Services/`) hold the
  use cases: one method per API operation. They use repository contracts and return
  `BaseResponse` / `BaseResponse<T>` with an `HttpStatusCode`.
- **DTOs** are the API's contract. Separate types for Create, Update and Read (CQRS at the contract level).
- **Validators** (FluentValidation) for every inbound DTO. They run in the API's `ValidationFilter`
  before the service is called.

```
SmartTask.Application/
├── Contracts/
│   ├── Persistence/   IRepository<T>, IProjectRepository, ITaskItemRepository,
│   │                  IUserRepository, IRefreshTokenRepository, IDashboardReadRepository, IUnitOfWork
│   ├── Services/      IAuthService, IUserService, IProjectService, ITaskService,
│   │                  IDashboardService, IAiService
│   ├── Identity/      ICurrentUserService, IJwtTokenService, IRefreshTokenService,
│   │                  IPasswordHasher, IRefreshTokenTransport
│   └── Ai/            ITaskDescriptionImprover
├── Common/
│   ├── Models/        PagedResult<T>, PageQuery, ProjectQueryParameters,
│   │                  TaskQueryParameters, UserQueryParameters, read models
│   ├── Responses/     IBaseResponse, BaseResponse, BaseResponse<T>
│   └── Exceptions/    ConflictException (raised by the UoW; services convert it to 409)
├── DTOs/<Feature>/    Create…Dto, Update…Dto, …Dto, …SummaryDto, …Mappings
├── Validators/<Feature>/
├── Services/          Auth/, Users/, Projects/, Tasks/, Dashboard/, Ai/, Authorization/ProjectAccessGuard
└── DependencyInjection.cs   AddApplication()
```

### SmartTask.Persistence
- `SmartTaskDbContext`, one `IEntityTypeConfiguration<T>` per entity.
- Repository and Unit of Work implementations.
- Interceptors for auditing and soft delete; global query filter for soft-deleted rows.
- Migrations.

```
SmartTask.Persistence/
├── Configurations/
├── Interceptors/       AuditableEntityInterceptor, SoftDeleteInterceptor
├── Extensions/         QueryableExtensions (paging, whitelisted sorting)
├── Repositories/       Repository<T>, ProjectRepository, TaskItemRepository, ...
├── Migrations/
├── SmartTaskDbContext.cs
├── UnitOfWork.cs
└── DependencyInjection.cs   AddPersistence(IConfiguration)
```

### SmartTask.Infrastructure
JWT generation/validation, refresh-token generation and hashing, password hashing
(`PasswordHasher<T>` from ASP.NET Core Identity or BCrypt), GitHub Models client, `TimeProvider`.

### SmartTask.Api
Thin controllers inheriting `BaseApiController` (maps `BaseResponse.Status` to the HTTP result),
`ValidationFilter`, global exception handler (`IExceptionHandler`), Serilog request logging, rate
limiting, CORS, HTTPS/HSTS, security headers, health checks, Swagger with JWT bearer support, and
the cookie transport for refresh tokens. Every response, errors included, uses the `BaseResponse` shape.

## Patterns in use (and why)

| Pattern            | Where                              | Why                                                     |
|--------------------|------------------------------------|---------------------------------------------------------|
| Repository         | Contracts/Persistence + Persistence| Keep EF Core out of use cases; enable unit testing      |
| Unit of Work       | `IUnitOfWork`                      | One `SaveChanges` per use case = one transaction         |
| Service layer      | Contracts/Services + Services/     | Use cases in one place; controllers stay thin           |
| CQRS (contracts)   | DTOs/                              | Separate read shapes from write rules                   |
| Result object      | `BaseResponse<T>`                  | Expected failures without exceptions; one response shape |
| Template method    | `BaseApiController.ToActionResult` | One status → HTTP mapping for all controllers           |
| Factory method     | Entity `Create(...)` methods       | Entities are always valid after construction            |
| Strategy           | `ITaskDescriptionImprover`, `IRefreshTokenTransport` | Swap AI provider / token transport without touching use cases |
| Filter (pipeline)  | `ValidationFilter`                 | Cross-cutting validation without duplicating code       |
| Options            | `JwtOptions`, `AiOptions`          | Strongly-typed, validated configuration                 |

## Cross-cutting decisions

- **Ids:** `Guid` (generated in the domain with `Guid.CreateVersion7()` for index-friendly ordering).
- **Time:** all timestamps in UTC as `DateTimeOffset`; inject `TimeProvider` instead of `DateTime.UtcNow`.
- **Soft delete** for `Project` and `TaskItem`; hard delete for refresh tokens.
- **Optimistic concurrency** via `RowVersion` on `Project` and `TaskItem` (server-side only).
- **Errors:** services return `BaseResponse` with 400 / 403 / 404 / 409 for expected outcomes (they
  catch `DomainException` and `ConflictException`). Validation → 400 from `ValidationFilter`.
  Anything unexpected → 500 from the global exception handler. See API-CONVENTIONS §2 and §4.
