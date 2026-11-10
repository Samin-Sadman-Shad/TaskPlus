using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using SmartTask.Api.Filters;
using SmartTask.Api.Identity;
using SmartTask.Api.Middleware;
using SmartTask.Application.Common.Responses;
using SmartTask.Application.Contracts.Identity;

namespace SmartTask.Api.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers everything the API host needs on top of the Application, Persistence and
    /// Infrastructure layers: controllers with the global <see cref="ValidationFilter"/> and JSON
    /// settings, the <see cref="BaseResponse"/> shape for binding errors and exceptions, the current-user
    /// and refresh-cookie adapters, authentication, authorization and rate limiting.
    /// </summary>
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
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

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IRefreshTokenTransport, CookieRefreshTokenTransport>();

        services.AddJwtAuthentication();
        services.AddApiAuthorization();
        services.AddApiRateLimiting(configuration);

        return services;
    }

    private static string ToCamelCasePath(string key) =>
        string.IsNullOrEmpty(key)
            ? key
            : string.Join('.', key.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}
