using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SmartTask.Application.Contracts.Identity;
using SmartTask.Infrastructure.Identity;

namespace SmartTask.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers token issuing, refresh-token generation and password hashing. <see cref="JwtOptions"/>
    /// is validated on startup, so the app won't start without a strong signing key.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();

        services.TryAddSingleton(TimeProvider.System);

        // Stateless and thread-safe, so singletons.
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IRefreshTokenService, RefreshTokenService>();
        services.AddSingleton<IPasswordHasher, PasswordHasherAdapter>();

        return services;
    }
}
