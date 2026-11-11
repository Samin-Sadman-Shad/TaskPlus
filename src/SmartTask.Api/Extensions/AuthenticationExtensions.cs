using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using SmartTask.Api.Authorization;
using SmartTask.Api.Http;
using SmartTask.Application.Common.Responses;
using SmartTask.Infrastructure.Identity;

namespace SmartTask.Api.Extensions;

public static class AuthenticationExtensions
{
    private static readonly TimeSpan AllowedClockSkew = TimeSpan.FromSeconds(30);

    /// <summary>
    /// JWT bearer authentication. Validation uses the same <see cref="JwtOptions"/> (and the same key)
    /// that issues the tokens. A challenge returns a <see cref="BaseResponse"/> body with an
    /// <see cref="ErrorCodes"/> value, like every other error.
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
                // 403s are written by ApiAuthorizationResultHandler, so only the challenge is customised here.
                bearer.Events = new JwtBearerEvents { OnChallenge = WriteUnauthorizedAsync };
            });

        return services;
    }

    /// <summary>
    /// Role-based authorization. The default policy (a bare <c>[Authorize]</c>) and the fallback policy
    /// (an endpoint with no attribute at all) both require an authenticated user with one of the known
    /// <see cref="AppRoles"/>, so a token with no role or an unknown role is refused everywhere.
    /// Endpoints narrow this with <c>[Authorize(Roles = AppRoles.X)]</c>; anonymous ones opt out with
    /// <c>[AllowAnonymous]</c>. Role failures are written by <see cref="ApiAuthorizationResultHandler"/>.
    /// </summary>
    public static IServiceCollection AddApiAuthorization(this IServiceCollection services)
    {
        var knownRoleUser = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireRole(AppRoles.All)
            .Build();

        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(knownRoleUser)
            .SetFallbackPolicy(knownRoleUser);

        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiAuthorizationResultHandler>();

        return services;
    }

    private static Task WriteUnauthorizedAsync(JwtBearerChallengeContext context)
    {
        // Replace the default empty 401 with the BaseResponse shape, but keep the standard header.
        context.HandleResponse();
        context.Response.Headers[HeaderNames.WWWAuthenticate] = JwtBearerDefaults.AuthenticationScheme;

        // The code lets the client tell "refresh the token" apart from "sign in again".
        var response = context.AuthenticateFailure switch
        {
            null => BaseResponse.Unauthorized(ResponseMessages.AuthenticationRequired, ErrorCodes.AuthenticationRequired),
            SecurityTokenExpiredException => BaseResponse.Unauthorized(ResponseMessages.TokenExpired, ErrorCodes.TokenExpired),
            _ => BaseResponse.Unauthorized(ResponseMessages.TokenInvalid, ErrorCodes.TokenInvalid),
        };

        return ApiResponseWriter.WriteAsync(context.HttpContext, response, context.HttpContext.RequestAborted);
    }
}
