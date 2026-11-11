using Microsoft.Data.SqlClient;
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

    /// <summary>Optional. Applied to the connection string, so the password never sits in a config file.</summary>
    public const string PasswordKey = "Database:Password";
    public const string MaxRetryCountKey = "Database:MaxRetryCount";
    public const string MaxRetryDelaySecondsKey = "Database:MaxRetryDelaySeconds";
    private const int DefaultMaxRetryCount = 6;
    private const int DefaultMaxRetryDelaySeconds = 30;

    /// <summary>
    /// Registers the DbContext (SQL Server with retry on transient failures), the interceptors, the
    /// repositories and the unit of work. Requires an <c>ICurrentUserService</c> registration from the API layer.
    /// When <c>Database:Password</c> is set (for example the <c>Database__Password</c> environment variable in
    /// Docker) it is applied to the connection string; otherwise the string is used unchanged (LocalDB).
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

        connectionString = ApplyPassword(connectionString, configuration[PasswordKey]);
        var maxRetryCount = ReadPositiveInt(configuration, MaxRetryCountKey, DefaultMaxRetryCount);
        var maxRetryDelay = TimeSpan.FromSeconds(ReadPositiveInt(configuration, MaxRetryDelaySecondsKey, DefaultMaxRetryDelaySeconds));

        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<SoftDeleteInterceptor>();
        services.AddScoped<AuditableEntityInterceptor>();

        services.AddDbContext<SmartTaskDbContext>((serviceProvider, options) => options
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(maxRetryCount, maxRetryDelay, errorNumbersToAdd: null))
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

    private static int ReadPositiveInt(IConfiguration configuration, string key, int fallback) =>
        int.TryParse(configuration[key], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : fallback;

    private static string ApplyPassword(string connectionString, string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return connectionString;
        }

        return new SqlConnectionStringBuilder(connectionString) { Password = password }.ConnectionString;
    }
}
