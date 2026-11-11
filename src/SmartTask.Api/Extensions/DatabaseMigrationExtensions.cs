using Microsoft.EntityFrameworkCore;
using SmartTask.Persistence;

namespace SmartTask.Api.Extensions;

/// <summary>
/// Applies pending EF Core migrations when the API starts, so a fresh container (<c>docker run</c> or
/// <c>docker compose up</c>) creates or upgrades its schema before it serves requests.
/// </summary>
/// <remarks>
/// Controlled by <c>Database:ApplyMigrationsOnStartup</c>. When the key is not set, migrations run only
/// inside a container: the official .NET images set <c>DOTNET_RUNNING_IN_CONTAINER=true</c>. A normal
/// <c>dotnet run</c> therefore never changes the schema by surprise; locally use <c>dotnet ef database update</c>.
/// EF Core takes a database-level migration lock, so several replicas starting together are safe.
/// A failure is not caught: the host stops with exit code 1 and the container restart policy retries.
/// </remarks>
public static class DatabaseMigrationExtensions
{
    public const string ApplyMigrationsOnStartupKey = "Database:ApplyMigrationsOnStartup";
    public const string RunningInContainerKey = "DOTNET_RUNNING_IN_CONTAINER";
    private const string LoggerCategory = "SmartTask.Api.DatabaseMigrations";

    /// <summary>The explicit setting wins; otherwise migrate only when running in a container.</summary>
    public static bool ShouldApplyMigrations(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return configuration.GetValue<bool?>(ApplyMigrationsOnStartupKey)
            ?? configuration.GetValue<bool>(RunningInContainerKey);
    }

    public static async Task ApplyDatabaseMigrationsAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (!ShouldApplyMigrations(app.Configuration))
        {
            return;
        }

        var cancellationToken = app.Lifetime.ApplicationStopping;
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(LoggerCategory);

        await using var scope = app.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<SmartTaskDbContext>().Database;

        var pending = (await database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count == 0)
        {
            logger.LogInformation("Database schema is up to date; no migrations to apply");
            return;
        }

        logger.LogInformation("Applying {Count} pending migration(s): {Migrations}", pending.Count, pending);
        await database.MigrateAsync(cancellationToken);
        logger.LogInformation("Database migrations applied");
    }
}
