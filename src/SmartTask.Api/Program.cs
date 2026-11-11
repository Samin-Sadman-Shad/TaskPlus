using Serilog;
using SmartTask.Api.Extensions;
using SmartTask.Application;
using SmartTask.Infrastructure;
using SmartTask.Persistence;

// Bootstrap logger: catches start-up failures before configuration is loaded. It is a plain logger
// (not CreateBootstrapLogger), so several hosts in one test process don't clash.
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, logger) => logger
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services
        .AddApplication()
        .AddPersistence(builder.Configuration)
        .AddInfrastructure(builder.Configuration)
        .AddApiServices(builder.Configuration, builder.Environment);

    var app = builder.Build();

    app.UseApiPipeline();
    await app.ApplyDatabaseMigrationsAsync();
    await app.SeedDevelopmentDataAsync();   // Development sample data (only into an empty database)
    await app.BootstrapAdminAsync();        // the deployment's first admin, if configured and none exists

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // HostAbortedException is how design-time tools (dotnet ef) stop the host on purpose; it is not a failure.
    Log.Fatal(ex, "The API terminated unexpectedly");
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>Exposed so integration tests can host the API with <c>WebApplicationFactory&lt;Program&gt;</c>.</summary>
public partial class Program;
