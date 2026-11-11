using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SmartTask.Infrastructure.Ai;

/// <summary>
/// Logs the active AI provider once at startup, with a warning when GitHub Models is configured but
/// <c>Ai:ApiKey</c> is missing (the fake provider then answers). Never logs the key.
/// </summary>
internal sealed partial class AiProviderStartupLogger(
    AiProviderSelection selection,
    IOptions<AiOptions> options,
    ILogger<AiProviderStartupLogger> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (selection.FellBackToFake)
        {
            LogFallback();
        }
        else if (selection.Active == AiProvider.GitHubModels)
        {
            LogActive(selection.Active, options.Value.Model);
        }
        else
        {
            LogActive(selection.Active, FakeTaskDescriptionImprover.ModelName);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Ai:Provider is GitHubModels but Ai:ApiKey is not set; using the fake AI provider. Set it with: dotnet user-secrets set \"Ai:ApiKey\" \"<github-pat-with-models-read>\" -p src/SmartTask.Api")]
    private partial void LogFallback();

    [LoggerMessage(Level = LogLevel.Information, Message = "AI provider {Provider} active (model {Model})")]
    private partial void LogActive(AiProvider provider, string model);
}
