using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SmartTask.Application.Contracts.Ai;
using SmartTask.Application.Contracts.Identity;
using SmartTask.Infrastructure.Ai;
using SmartTask.Infrastructure.Identity;

namespace SmartTask.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers token issuing, refresh-token generation, password hashing and the AI provider.
    /// <see cref="JwtOptions"/> and <see cref="AiOptions"/> are validated on startup, so the app won't
    /// start without a strong signing key or with a broken AI configuration.
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

        services.AddAi(configuration);

        return services;
    }

    /// <summary>
    /// Picks the <see cref="ITaskDescriptionImprover"/> (Strategy): GitHub Models when <c>Ai:Provider</c> is
    /// <c>GitHubModels</c> and <c>Ai:ApiKey</c> is set, otherwise the fake provider, so the app and the
    /// endpoint work without a key. The choice is made once, from configuration, at startup.
    /// </summary>
    private static void AddAi(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(AiOptions.SectionName);

        services.AddOptions<AiOptions>()
            .Bind(section)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<AiOptions>, AiOptionsValidator>();

        var selection = AiProviderSelection.From(section.Get<AiOptions>() ?? new AiOptions());
        services.AddSingleton(selection);
        services.AddHostedService<AiProviderStartupLogger>();

        if (selection.Active == AiProvider.GitHubModels)
        {
            services.AddKeyedSingleton<IChatClient>(
                GitHubModelsTaskDescriptionImprover.ChatClientKey,
                (provider, _) => GitHubModelsTaskDescriptionImprover.CreateChatClient(provider.GetRequiredService<IOptions<AiOptions>>().Value));
            services.AddSingleton<ITaskDescriptionImprover, GitHubModelsTaskDescriptionImprover>();
        }
        else
        {
            services.AddSingleton<ITaskDescriptionImprover, FakeTaskDescriptionImprover>();
        }
    }
}
