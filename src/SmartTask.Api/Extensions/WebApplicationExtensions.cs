namespace SmartTask.Api.Extensions;

public static class WebApplicationExtensions
{
    /// <summary>
    /// The request pipeline. <c>UseRateLimiter</c> comes after <c>UseAuthentication</c> so the global
    /// limiter can partition by user id. <c>/create-api</c> adds HSTS, security headers, request logging,
    /// Swagger, CORS and health checks.
    /// </summary>
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseExceptionHandler();
        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapControllers();

        return app;
    }
}
