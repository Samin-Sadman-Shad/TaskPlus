using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SmartTask.Application.Common.Responses;

namespace SmartTask.Api.Http;

/// <summary>
/// Writes a <see cref="IBaseResponse"/> from places outside MVC (authentication challenges, rate
/// limiter rejections, the exception handler), using the same JSON settings as the controllers. This
/// way every error has the same shape.
/// </summary>
public static class ApiResponseWriter
{
    public static Task WriteAsync(HttpContext context, IBaseResponse response, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(response);

        response.TraceId ??= context.TraceIdentifier;
        context.Response.StatusCode = (int)response.Status;

        var json = context.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;
        return context.Response.WriteAsJsonAsync(response, response.GetType(), json, cancellationToken);
    }
}
