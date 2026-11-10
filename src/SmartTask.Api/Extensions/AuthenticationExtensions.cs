using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using SmartTask.Api.Authorization;
using SmartTask.Api.Http;
using SmartTask.Application.Common.Responses;
using SmartTask.Domain.Enums;
using SmartTask.Infrastructure.Identity;

namespace SmartTask.Api.Extensions;

public static class AuthenticationExtensions
{
    private static readonly TimeSpan AllowedClockSkew = TimeSpan.FromSeconds(30);

    /// <summary>
    /// JWT bearer authentication. Validation uses the same <see cref="JwtOptions"/> (and the same key)
    /// that issues the tokens. Challenges and forbidden results return a <see cref="BaseResponse"/>
    /// body, like every other error.
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;

                // Keep the short claim names ("sub", "role") instead of mapping them to long URIs.
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = jwt.CreateSigningKey(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = AllowedClockSkew,
                    NameClaimType = AppClaimTypes.Name,
                    RoleClaimType = AppClaimTypes.Role,
                };
                bearer.Events = new JwtBearerEvents
                {
                    OnChallenge = WriteUnauthorizedAsync,
                    OnForbidden = context => ApiResponseWriter.WriteAsync(
                        context.HttpContext,
                        BaseResponse.Forbidden("You do not have permission to perform this action."),
                        context.HttpContext.RequestAborted),
                };
            });

        return services;
    }

    /// <summary>
    /// Every endpoint requires an authenticated user unless it opts out with <c>[AllowAnonymous]</c>.
    /// Role policies: <see cref="Policies.AdminOnly"/> and <see cref="Policies.ProjectManagement"/>.
    /// </summary>
    public static IServiceCollection AddApiAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.AdminOnly, policy => policy.RequireRole(nameof(UserRole.Admin)))
            .AddPolicy(Policies.ProjectManagement, policy => policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.ProjectManager)));

        return services;
    }

    private static Task WriteUnauthorizedAsync(JwtBearerChallengeContext context)
    {
        // Replace the default empty 401 with the BaseResponse shape, but keep the standard header.
        context.HandleResponse();
        context.Response.Headers[HeaderNames.WWWAuthenticate] = JwtBearerDefaults.AuthenticationScheme;

        var message = context.AuthenticateFailure is SecurityTokenExpiredException
            ? "The access token has expired."
            : ResponseMessages.AuthenticationRequired;

        return ApiResponseWriter.WriteAsync(context.HttpContext, BaseResponse.Unauthorized(message), context.HttpContext.RequestAborted);
    }
}
