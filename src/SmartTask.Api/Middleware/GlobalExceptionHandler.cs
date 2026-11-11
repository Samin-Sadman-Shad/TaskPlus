using Microsoft.AspNetCore.Diagnostics;
using SmartTask.Api.Http;
using SmartTask.Application.Common.Exceptions;
using SmartTask.Application.Common.Responses;
using SmartTask.Domain.Exceptions;

namespace SmartTask.Api.Middleware;

/// <summary>
/// The last line of defence (API-CONVENTIONS §4). A <see cref="DomainException"/> that escaped a
/// service becomes 400, a <see cref="ConflictException"/> 409, a <see cref="ForbiddenAccessException"/>
/// 403 and an <see cref="UnauthenticatedException"/> 401 (both with an <see cref="ErrorCodes"/> value,
/// logged at Warning), and anything else 500 with a generic message. Details stay in the log, tagged with the trace id the client also receives.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public const string UnexpectedErrorMessage = "An unexpected error occurred.";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            // The client went away; there is nobody to answer.
            logger.LogDebug("Request {TraceId} was cancelled by the client", httpContext.TraceIdentifier);
            return true;
        }

        BaseResponse response = exception switch
        {
            DomainException domain => BaseResponse.BadRequest(domain.Message),
            ConflictException conflict => BaseResponse.Conflict(conflict.Message),
            ForbiddenAccessException forbidden => BaseResponse.Forbidden(forbidden.Message, forbidden.ErrorCode),
            UnauthenticatedException unauthenticated => BaseResponse.Unauthorized(unauthenticated.Message, ErrorCodes.AuthenticationRequired),
            BadHttpRequestException badRequest => BaseResponse.Fail((System.Net.HttpStatusCode)badRequest.StatusCode, "The request could not be processed."),
            _ => BaseResponse.Error(UnexpectedErrorMessage),
        };

        if (response.StatusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path} (trace {TraceId})",
                httpContext.Request.Method, httpContext.Request.Path, httpContext.TraceIdentifier);
        }
        else
        {
            logger.LogWarning("Request {TraceId} failed with {StatusCode}: {Reason}",
                httpContext.TraceIdentifier, response.StatusCode, exception.Message);
        }

        await ApiResponseWriter.WriteAsync(httpContext, response, cancellationToken);
        return true;
    }
}
