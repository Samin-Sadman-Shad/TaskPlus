using System.Net;
using Microsoft.Extensions.Logging;
using SmartTask.Application.Common.Exceptions;
using SmartTask.Domain.Exceptions;

namespace SmartTask.Application.Services;

/// <summary>
/// Turns the two <b>expected</b> failure exceptions into responses, so each write method doesn't
/// repeat the same try/catch:
/// <list type="bullet">
/// <item><see cref="DomainException"/>: a broken invariant or illegal transition, becomes 400.</item>
/// <item><see cref="ConflictException"/>: a concurrency or unique-index conflict from the unit of work, becomes 409.</item>
/// </list>
/// Any other exception propagates to the global exception handler (500).
/// </summary>
internal static class BusinessErrorHandling
{
    public static async Task<TResponse> RunAsync<TResponse>(
        Func<Task<TResponse>> operation,
        Func<HttpStatusCode, string, TResponse> fail,
        ILogger logger)
    {
        try
        {
            return await operation();
        }
        catch (DomainException ex)
        {
            logger.LogInformation("Business rule rejected the request: {Reason}", ex.Message);
            return fail(HttpStatusCode.BadRequest, ex.Message);
        }
        catch (ConflictException ex)
        {
            logger.LogWarning("Save conflict: {Reason}", ex.Message);
            return fail(HttpStatusCode.Conflict, ex.Message);
        }
    }
}
