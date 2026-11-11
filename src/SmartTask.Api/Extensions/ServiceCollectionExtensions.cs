using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using SmartTask.Api.Filters;
using SmartTask.Api.Identity;
using SmartTask.Api.Middleware;
using SmartTask.Application.Common.Responses;
using SmartTask.Application.Contracts.Identity;
using SmartTask.Persistence.Seed;

namespace SmartTask.Api.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>Largest request body accepted (1 MB). The API only takes small JSON documents.</summary>
    public const long MaxRequestBodyBytes = 1024 * 1024;

    /// <summary>
    /// Registers everything the API host needs on top of the Application, Persistence and
    /// Infrastructure layers:
    /// <list type="bullet">
    /// <item>controllers, with the global <see cref="ValidationFilter"/> and the JSON settings;</item>
    /// <item>the <see cref="BaseResponse"/> shape for binding errors and exceptions;</item>
    /// <item>the current-user and refresh-cookie adapters;</item>
    /// <item>authentication, authorization, rate limiting, CORS, Swagger and health checks.</item>
    /// </list>
    /// </summary>
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services
            .AddControllers(options =>
            {
                options.Filters.Add<ValidationFilter>();

                // FluentValidation is the single source of validation rules. Without this, MVC would add
                // an implicit [Required] to every non-nullable string property and answer before the validators.
                options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        services.Configure<ApiBehaviorOptions>(options =>
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(entry => entry.Value is { Errors.Count: > 0 })
                    .ToDictionary(
                        entry => ToCamelCasePath(entry.Key),
                        entry => entry.Value!.Errors
                            .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "The value is invalid." : e.ErrorMessage)
                            .ToArray());

                var response = BaseResponse.ValidationFailed(errors);
                response.TraceId = context.HttpContext.TraceIdentifier;
                return new BadRequestObjectResult(response);
            });

        services.Configure<KestrelServerOptions>(options => options.Limits.MaxRequestBodySize = MaxRequestBodyBytes);

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IRefreshTokenTransport, CookieRefreshTokenTransport>();
        services.AddApiForwardedHeaders(configuration);

        services.AddJwtAuthentication();
        services.AddApiAuthorization();
        services.AddApiRateLimiting(configuration);
        services.AddFrontendCors(configuration);
        services.AddApiHealthChecks();

        if (SwaggerExtensions.IsSwaggerEnabled(configuration, environment))
        {
            services.AddApiSwagger();
        }

        if (environment.IsDevelopment())
        {
            services.AddScoped<DevelopmentDataSeeder>();
        }

        return services;
    }

    private static string ToCamelCasePath(string key) =>
        string.IsNullOrEmpty(key)
            ? key
            : string.Join('.', key.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}
