using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartTask.Persistence;
using SmartTask.Persistence.Interceptors;

namespace SmartTask.Api.IntegrationTests;

/// <summary>
/// Hosts the real API pipeline in memory. SQL Server is replaced by an in-memory SQLite database (one
/// connection kept open for the factory's lifetime, so the schema survives between requests). The
/// interceptors are kept, so auditing and soft delete behave as in production.
/// </summary>
public sealed class SmartTaskApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Test-only signing key (≥ 32 bytes). Never used outside these tests.</summary>
    private const string TestSigningKey = "integration-test-signing-key-0123456789-abcdefghijklmnop";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly string _logDirectory = Path.Combine(Path.GetTempPath(), "smarttask-it-logs", Guid.NewGuid().ToString("N"));

    public static readonly Uri BaseAddress = new("https://localhost");

    public HttpClient CreateApiClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = BaseAddress,
        AllowAutoRedirect = false,
        HandleCookies = true,
    });

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SmartTaskDbContext>().Database.EnsureCreatedAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _connection.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Placeholder only: AddPersistence requires a connection string, but the provider is replaced below.
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=unused;Database=unused");
        builder.UseSetting("Jwt:SigningKey", TestSigningKey);
        builder.UseSetting("Swagger:Enabled", "true");
        builder.UseSetting("RateLimiting:Auth:PermitLimit", "1000");
        builder.UseSetting("RateLimiting:Global:PermitLimit", "1000");
        // Keep test log files out of the source tree.
        builder.UseSetting("Serilog:WriteTo:1:Args:path", Path.Combine(_logDirectory, "test-.log"));

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<SmartTaskDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<SmartTaskDbContext>>();
            services.AddDbContext<SmartTaskDbContext>((provider, options) => options
                .UseSqlite(_connection)
                .ReplaceService<IModelCustomizer, SqliteModelCustomizer>()
                .AddInterceptors(
                    provider.GetRequiredService<SoftDeleteInterceptor>(),
                    provider.GetRequiredService<AuditableEntityInterceptor>()));
        });
    }
}

/// <summary>One shared host for all integration test classes (starting a host is the expensive part).</summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<SmartTaskApiFactory>
{
    public const string Name = "api";
}
