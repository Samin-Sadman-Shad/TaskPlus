namespace SmartTask.Api.Extensions;

public static class CorsExtensions
{
    public const string FrontendPolicy = "Frontend";
    public const string AllowedOriginsKey = "Cors:AllowedOrigins";

    private static readonly TimeSpan PreflightCacheDuration = TimeSpan.FromMinutes(10);

    /// <summary>
    /// The <c>Frontend</c> policy: only the origins listed in <c>Cors:AllowedOrigins</c> (never a
    /// wildcard), with credentials allowed so the browser sends the refresh-token cookie, and only the
    /// methods and headers the client uses. An empty list allows no cross-origin calls at all.
    /// </summary>
    public static IServiceCollection AddFrontendCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection(AllowedOriginsKey).Get<string[]>() ?? [];

        services.AddCors(options => options.AddPolicy(FrontendPolicy, policy => policy
            .WithOrigins(origins)
            .AllowCredentials()
            .WithMethods(HttpMethods.Get, HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete)
            .WithHeaders("Content-Type", "Authorization")
            .WithExposedHeaders("Location", "Retry-After")
            .SetPreflightMaxAge(PreflightCacheDuration)));

        return services;
    }
}
