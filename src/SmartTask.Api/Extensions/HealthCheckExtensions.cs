using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SmartTask.Persistence;

namespace SmartTask.Api.Extensions;

public static class HealthCheckExtensions
{
    public const string LivePath = "/health/live";
    public const string ReadyPath = "/health/ready";
    private const string ReadyTag = "ready";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>The readiness check asks SQL Server whether it can connect. Liveness has no checks.</summary>
    public static IServiceCollection AddApiHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks().AddDbContextCheck<SmartTaskDbContext>(name: "database", tags: [ReadyTag]);
        return services;
    }

    /// <summary>
    /// <c>/health/live</c>: the process is up. <c>/health/ready</c>: the database is reachable. Both are
    /// anonymous and exempt from rate limiting, so probes and load balancers can call them freely.
    /// </summary>
    public static WebApplication MapApiHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks(LivePath, new HealthCheckOptions { Predicate = _ => false, ResponseWriter = WriteJsonAsync })
            .AllowAnonymous()
            .DisableRateLimiting();

        app.MapHealthChecks(ReadyPath, new HealthCheckOptions { Predicate = c => c.Tags.Contains(ReadyTag), ResponseWriter = WriteJsonAsync })
            .AllowAnonymous()
            .DisableRateLimiting();

        return app;
    }

    /// <summary>Status and timings only. Exception details are not exposed to anonymous callers.</summary>
    private static Task WriteJsonAsync(HttpContext context, HealthReport report)
    {
        var body = new
        {
            status = report.Status.ToString(),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                durationMs = Math.Round(e.Value.Duration.TotalMilliseconds, 1),
            }),
        };

        return context.Response.WriteAsJsonAsync(body, JsonOptions);
    }
}
