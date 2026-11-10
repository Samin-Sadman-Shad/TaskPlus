using SmartTask.Api.Extensions;

namespace SmartTask.Api.Middleware;

/// <summary>
/// Adds defensive headers to every response. The API only returns JSON, so the CSP forbids everything.
/// The Swagger UI is HTML with scripts, so it doesn't get the CSP (it still gets the other headers).
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string ApiContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";

            if (!context.Request.Path.StartsWithSegments(SwaggerExtensions.UiPath))
            {
                headers.ContentSecurityPolicy = ApiContentSecurityPolicy;
            }

            return Task.CompletedTask;
        });

        return next(context);
    }
}
