using Microsoft.OpenApi;

namespace SmartTask.Api.Extensions;

/// <summary>
/// Swagger with Swashbuckle (OpenAPI document plus Swagger UI), with XML comments and a JWT bearer
/// scheme, so "Authorize" works in the UI. The UI is served in Development, or wherever
/// <c>Swagger:Enabled</c> is true.
/// </summary>
public static class SwaggerExtensions
{
    public const string UiPath = "/swagger";
    private const string BearerSchemeId = "Bearer";

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

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(BearerSchemeId, document)] = [],
            });
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
}
