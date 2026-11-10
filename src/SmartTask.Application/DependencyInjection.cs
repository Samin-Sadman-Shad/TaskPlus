using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SmartTask.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the Application layer: every FluentValidation validator in this assembly. The API's
    /// global <c>ValidationFilter</c> resolves them per request argument.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        return services;
    }
}
