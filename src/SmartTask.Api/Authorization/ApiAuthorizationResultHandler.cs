using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authorization.Policy;
using SmartTask.Api.Http;
using SmartTask.Application.Common.Responses;
using SmartTask.Infrastructure.Identity;

namespace SmartTask.Api.Authorization;

/// <summary>
/// The single place where a failed role check becomes an HTTP response. A forbidden result
/// (authenticated, wrong or unknown role) gets a 403 <see cref="BaseResponse"/> with
/// <see cref="ErrorCodes.InsufficientRole"/>. Everything else (success, and the challenge for an
/// unauthenticated caller) goes to the framework's default handler, so challenges still reach
/// <c>JwtBearerEvents.OnChallenge</c>.
/// </summary>
/// <remarks>
/// This is the framework's global hook for authorization results, so no custom middleware is needed.
/// </remarks>
public sealed class ApiAuthorizationResultHandler(ILogger<ApiAuthorizationResultHandler> logger)
    : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(authorizeResult);

        if (!authorizeResult.Forbidden)
        {
            await _default.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        if (logger.IsEnabled(LogLevel.Warning))
        {
            var requiredRoles = authorizeResult.AuthorizationFailure?.FailedRequirements
                .OfType<RolesAuthorizationRequirement>()
                .SelectMany(requirement => requirement.AllowedRoles)
                .Distinct()
                .ToArray() ?? [];

            // Ids and role names only: never the token or the email.
            logger.LogWarning(
                "Forbidden {Method} {Path} for user {UserId} with role {Role}; required one of {RequiredRoles} (trace {TraceId})",
                context.Request.Method,
                context.Request.Path,
                context.User.FindFirstValue(AppClaimTypes.Subject),
                context.User.FindFirstValue(AppClaimTypes.Role),
                requiredRoles,
                context.TraceIdentifier);
        }

        await ApiResponseWriter.WriteAsync(
            context,
            BaseResponse.Forbidden(ResponseMessages.InsufficientRole, ErrorCodes.InsufficientRole),
            context.RequestAborted);
    }
}
