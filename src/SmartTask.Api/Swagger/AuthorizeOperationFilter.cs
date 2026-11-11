using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SmartTask.Api.Swagger;

/// <summary>
/// Puts the Bearer requirement (the padlock in Swagger UI) only on endpoints that need a token, and
/// lists the roles that may call each one. Anonymous endpoints (<c>[AllowAnonymous]</c>: auth, health)
/// get no padlock.
/// </summary>
/// <remarks>
/// It reads the same <c>[Authorize]</c> / <c>[AllowAnonymous]</c> metadata the authorization middleware
/// uses, so the documentation can't drift from the rules. It never changes authorization behaviour.
/// Every <c>[Authorize(Roles = …)]</c> on the controller and the action must pass, so the effective
/// roles are their intersection.
/// </remarks>
public sealed class AuthorizeOperationFilter : IOperationFilter
{
    public const string BearerSchemeId = "Bearer";

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAllowAnonymous>().Any())
        {
            return;
        }

        var authorizeData = metadata.OfType<IAuthorizeData>().ToList();
        if (authorizeData.Count == 0)
        {
            return; // covered by the fallback policy; still needs a token
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(BearerSchemeId, context.Document)] = [],
        });

        var roles = EffectiveRoles(authorizeData);
        if (roles.Count > 0)
        {
            var line = $"**Roles:** {string.Join(", ", roles)}";
            operation.Description = string.IsNullOrWhiteSpace(operation.Description)
                ? line
                : $"{operation.Description}\n\n{line}";
        }
    }

    private static List<string> EffectiveRoles(IEnumerable<IAuthorizeData> authorizeData)
    {
        HashSet<string>? effective = null;
        foreach (var data in authorizeData.Where(d => !string.IsNullOrWhiteSpace(d.Roles)))
        {
            var roles = data.Roles!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (effective is null)
            {
                effective = new HashSet<string>(roles, StringComparer.Ordinal);
            }
            else
            {
                effective.IntersectWith(roles);
            }
        }

        return effective is null ? [] : [.. effective.Order(StringComparer.Ordinal)];
    }
}
