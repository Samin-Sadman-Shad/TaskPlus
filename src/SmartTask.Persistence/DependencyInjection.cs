using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartTask.Application.Contracts.Persistence;
using SmartTask.Persistence.Interceptors;
using SmartTask.Persistence.Repositories;

namespace SmartTask.Persistence;

public static class DependencyInjection
{
    public const string ConnectionStringName = "DefaultConnection";

    /// <summary>
    /// Registers the DbContext (SQL Server with retry on transient failures), the interceptors, the
    /// repositories and the unit of work. Requires an <c>ICurrentUserService</c> registration from the API layer.
    /// </summary>
    /// <exception cref="InvalidOperationException">The <c>DefaultConnection</c> connection string is missing.</exception>
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured. Set it with " +
                $"'dotnet user-secrets set \"ConnectionStrings:{ConnectionStringName}\" \"...\"' locally " +
                $"or the 'ConnectionStrings__{ConnectionStringName}' environment variable.");
        }

        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<SoftDeleteInterceptor>();
        services.AddScoped<AuditableEntityInterceptor>();

        services.AddDbContext<SmartTaskDbContext>((serviceProvider, options) => options
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())
            // Order matters: soft delete first, so the audit interceptor stamps it as an update.
            .AddInterceptors(
                serviceProvider.GetRequiredService<SoftDeleteInterceptor>(),
                serviceProvider.GetRequiredService<AuditableEntityInterceptor>())
            // ProjectMember has no filter of its own; it is only reached through its (filtered) project.
            .ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<ITaskItemRepository, TaskItemRepository>();
        services.AddScoped<IDashboardReadRepository, DashboardReadRepository>();

        return services;
    }
}
