# Architecture

## Style

**Clean Architecture** with a **CQRS** split in the Application layer. The domain is the centre;
everything else depends inward.

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
- **CQRS**: commands change state and return minimal results (id or read DTO); queries never change
  state and return read DTOs.
- **DTOs** are the API's contract. Separate types for Create, Update and Read.
- **Validators** (FluentValidation) for every inbound DTO.

```
SmartTask.Application/
├── Contracts/
│   ├── Persistence/   IRepository<T>, IProjectRepository, ITaskItemRepository,
│   │                  IUserRepository, IRefreshTokenRepository, IUnitOfWork
│   ├── Identity/      ICurrentUserService, IJwtTokenService, IPasswordHasher
│   └── Ai/            ITaskDescriptionImprover
├── Common/
│   ├── Models/        PagedResult<T>, PageQuery, ProjectQueryParameters,
│   │                  TaskQueryParameters, ApiResponse<T>, read models
│   └── Exceptions/    NotFoundException, ForbiddenException, ConflictException
├── DTOs/<Feature>/    Create…Dto, Update…Dto, …Dto, …SummaryDto, …Mappings
├── Validators/<Feature>/
├── Features/<Feature>/Commands|Queries/
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
Thin controllers, global exception handler (`IExceptionHandler` + ProblemDetails), Serilog request
logging, rate limiting, CORS, HTTPS/HSTS, health checks, Swagger with JWT bearer support,
consistent `ApiResponse<T>` envelope.

## Patterns in use (and why)

| Pattern            | Where                              | Why                                                     |
|--------------------|------------------------------------|---------------------------------------------------------|
| Repository         | Contracts/Persistence + Persistence| Keep EF Core out of use cases; enable unit testing      |
| Unit of Work       | `IUnitOfWork`                      | One `SaveChanges` per use case = one transaction         |
| CQRS               | Features/                          | Separate read shapes from write rules                   |
| Factory method     | Entity `Create(...)` methods       | Entities are always valid after construction            |
| Strategy           | `ITaskDescriptionImprover`         | Swap AI provider without touching use cases             |
| Decorator/Pipeline | Validation step before handlers    | Cross-cutting validation without duplicating code       |
| Options            | `JwtOptions`, `AiOptions`          | Strongly-typed, validated configuration                 |

## Cross-cutting decisions

- **Ids:** `Guid` (generated in the domain with `Guid.CreateVersion7()` for index-friendly ordering).
- **Time:** all timestamps in UTC as `DateTimeOffset`; inject `TimeProvider` instead of `DateTime.UtcNow`.
- **Soft delete** for `Project` and `TaskItem`; hard delete for refresh tokens.
- **Optimistic concurrency** via `RowVersion` on `Project` and `TaskItem` (server-side only).
- **Errors:** domain → `DomainException` (400), missing → `NotFoundException` (404),
  permission → `ForbiddenException` (403), duplicates → `ConflictException` (409), validation → 400
  with field errors.
