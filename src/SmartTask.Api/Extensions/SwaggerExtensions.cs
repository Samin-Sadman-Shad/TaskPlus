using System.Text;
using Microsoft.OpenApi;
using SmartTask.Api.Swagger;

namespace SmartTask.Api.Extensions;

/// <summary>
/// Swagger with Swashbuckle (OpenAPI document plus Swagger UI), with XML comments and a JWT bearer
/// scheme, so "Authorize" works in the UI. <see cref="AuthorizeOperationFilter"/> puts the padlock and
/// the allowed roles only on endpoints that need a token. The UI is served in Development, or
/// wherever <c>Swagger:Enabled</c> is true (the Docker environment sets it).
/// </summary>
public static class SwaggerExtensions
{
    public const string UiPath = "/swagger";
    private const string BearerSchemeId = AuthorizeOperationFilter.BearerSchemeId;

    public static bool IsSwaggerEnabled(IConfiguration configuration, IHostEnvironment environment) =>
        environment.IsDevelopment() || configuration.GetValue<bool>("Swagger:Enabled");

    public static IServiceCollection AddApiSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Smart Task Management API",
                Version = "v1",
                Description = "Projects, tasks, users and dashboard. Every response uses the BaseResponse envelope.",
            });

            var xmlFile = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
            if (File.Exists(xmlFile))
            {
                options.IncludeXmlComments(xmlFile, includeControllerXmlComments: true);
            }

            options.AddSecurityDefinition(BearerSchemeId, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste the accessToken from POST /api/v1/auth/login (without the 'Bearer ' prefix).",
            });

            options.OperationFilter<AuthorizeOperationFilter>();
            options.CustomSchemaIds(GenerateSchemaId);
        });

        return services;
    }

    public static WebApplication UseApiSwagger(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Smart Task Management API v1");
            options.DocumentTitle = "Smart Task Management API";
        });

        return app;
    }

    /// <summary>
    /// Readable schema ids for generic types: <c>BaseResponse&lt;UserDto&gt;</c> and
    /// <c>PagedResult&lt;TaskSummaryDto&gt;</c> instead of <c>UserDtoBaseResponse</c>.
    /// </summary>
    /// <remarks>
    /// <c>&lt;</c>, <c>&gt;</c> and <c>,</c> fall outside the OpenAPI component-name pattern
    /// (<c>^[a-zA-Z0-9.\-_]+$</c>). Swagger UI handles them, but some importers (code generators,
    /// Postman) may not: <c>/write-docs</c> must check that the exported collection still imports.
    /// </remarks>
    private static string GenerateSchemaId(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.Name;
        }

        var sb = new StringBuilder(RemoveGenericTypeParameterCount(type.GetGenericTypeDefinition().Name)).Append('<');
        var arguments = type.GetGenericArguments();
        for (var i = 0; i < arguments.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append(GenerateSchemaId(arguments[i]));
        }

        return sb.Append('>').ToString();
    }

    /// <summary>"BaseResponse`1" → "BaseResponse".</summary>
    private static string RemoveGenericTypeParameterCount(string typeName)
    {
        var backtick = typeName.IndexOf('`', StringComparison.Ordinal);
        return backtick >= 0 ? typeName[..backtick] : typeName;
    }
}
