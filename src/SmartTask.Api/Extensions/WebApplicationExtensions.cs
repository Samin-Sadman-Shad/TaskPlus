using System.Security.Claims;
using Serilog;
using Serilog.Context;
using SmartTask.Api.Middleware;
using SmartTask.Infrastructure.Identity;
using SmartTask.Persistence.Seed;

namespace SmartTask.Api.Extensions;

public static class WebApplicationExtensions
{
    /// <summary>
    /// The request pipeline, in this order:
    /// <list type="number">
    /// <item>exception handler, HSTS (outside Development), HTTPS redirection, security headers;</item>
    /// <item>request logging, Swagger (when enabled), CORS;</item>
    /// <item>authentication, then the user id is pushed into the log context, then authorization;</item>
    /// <item>rate limiter, then controllers and health checks.</item>
    /// </list>
    /// The rate limiter comes after authentication, so the global limiter can partition by user id.
    /// </summary>
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseExceptionHandler();
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseMiddleware<SecurityHeadersMiddleware>();

        app.UseSerilogRequestLogging(options => options.EnrichDiagnosticContext = (diagnostics, http) =>
        {
            diagnostics.Set("TraceId", http.TraceIdentifier);
            diagnostics.Set("UserId", UserIdOf(http));
        });

        if (SwaggerExtensions.IsSwaggerEnabled(app.Configuration, app.Environment))
        {
            app.UseApiSwagger();
        }

        app.UseCors(CorsExtensions.FrontendPolicy);
        app.UseAuthentication();
        app.Use(async (http, next) =>
        {
            // Every log event written during the request carries the caller's id and the trace id.
            using (LogContext.PushProperty("UserId", UserIdOf(http)))
            using (LogContext.PushProperty("TraceId", http.TraceIdentifier))
            {
                await next(http);
            }
        });
        app.UseAuthorization();
        app.UseRateLimiter();

        app.MapControllers();
        app.MapApiHealthChecks();

        return app;
    }

    /// <summary>
    /// Development only, and only when <c>Seed:Enabled</c> is true: applies migrations and seeds
    /// sample data. It never runs (and never migrates) in any other environment.
    /// </summary>
    public static async Task SeedDevelopmentDataAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (!app.Environment.IsDevelopment() || !app.Configuration.GetValue<bool>(DevelopmentDataSeeder.EnabledKey))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>().SeedAsync(app.Lifetime.ApplicationStopping);
    }

    private static string? UserIdOf(HttpContext http) => http.User.FindFirstValue(AppClaimTypes.Subject);
}
