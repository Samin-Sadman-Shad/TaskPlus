using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace SmartTask.Api.Extensions;

/// <summary>
/// Trusts <c>X-Forwarded-For</c> only from the reverse proxies listed in <c>ForwardedHeaders:KnownProxies</c>
/// (exact IP addresses), so the client's real IP reaches the rate limiter and the refresh-token audit fields.
/// With no proxies configured the middleware isn't added at all, and a request can never choose its own IP
/// by sending the header.
/// <para>
/// <c>X-Forwarded-Proto</c> is deliberately ignored: the proxy calls the API over HTTPS, so the request scheme
/// is already right. Taking the browser's scheme instead (http on localhost) would make HTTPS redirection
/// answer every proxied call with a redirect.
/// </para>
/// </summary>
public static class ForwardedHeadersExtensions
{
    public const string KnownProxiesKey = "ForwardedHeaders:KnownProxies";

    /// <summary>Reads and validates the configured proxy addresses. Throws on an invalid entry, so a typo fails start-up.</summary>
    public static IReadOnlyList<IPAddress> ReadKnownProxies(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var values = configuration.GetSection(KnownProxiesKey).Get<string[]>() ?? [];
        return values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => IPAddress.TryParse(v.Trim(), out var address)
                ? address
                : throw new InvalidOperationException($"{KnownProxiesKey} contains '{v}', which is not an IP address."))
            .ToList();
    }

    public static IServiceCollection AddApiForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        var proxies = ReadKnownProxies(configuration);
        if (proxies.Count == 0)
        {
            return services;
        }

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
            options.ForwardLimit = 1;
            // Drop the loopback defaults: only the listed proxies are trusted.
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            foreach (var proxy in proxies)
            {
                options.KnownProxies.Add(proxy);
            }
        });
        return services;
    }

    /// <summary>Must run first, before anything reads the client IP or scheme.</summary>
    public static WebApplication UseApiForwardedHeaders(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (ReadKnownProxies(app.Configuration).Count > 0)
        {
            app.UseForwardedHeaders();
        }
        return app;
    }
}
