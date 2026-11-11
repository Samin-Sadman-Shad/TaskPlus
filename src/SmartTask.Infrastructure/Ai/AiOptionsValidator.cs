using Microsoft.Extensions.Options;

namespace SmartTask.Infrastructure.Ai;

/// <summary>
/// Runs at startup (<c>ValidateOnStart</c>). A missing key is <b>not</b> an error: the app then falls back to
/// the fake provider (see <c>DependencyInjection.AddAi</c>). Messages name the setting, never its value.
/// </summary>
public sealed class AiOptionsValidator : IValidateOptions<AiOptions>
{
    public const int MinTimeoutSeconds = 5;
    public const int MaxTimeoutSeconds = 60;
    public const int MinOutputTokens = 50;
    public const int MaxOutputTokensLimit = 4000;

    public ValidateOptionsResult Validate(string? name, AiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();

        if (!Enum.IsDefined(options.Provider))
        {
            failures.Add($"Ai:Provider must be one of: {string.Join(", ", Enum.GetNames<AiProvider>())}.");
        }

        if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps)
        {
            failures.Add("Ai:Endpoint must be an absolute https URI.");
        }

        if (string.IsNullOrWhiteSpace(options.Model))
        {
            failures.Add("Ai:Model is required.");
        }

        if (options.TimeoutSeconds is < MinTimeoutSeconds or > MaxTimeoutSeconds)
        {
            failures.Add($"Ai:TimeoutSeconds must be between {MinTimeoutSeconds} and {MaxTimeoutSeconds}.");
        }

        if (options.MaxOutputTokens is < MinOutputTokens or > MaxOutputTokensLimit)
        {
            failures.Add($"Ai:MaxOutputTokens must be between {MinOutputTokens} and {MaxOutputTokensLimit}.");
        }

        if (float.IsNaN(options.Temperature) || options.Temperature is < 0f or > 1f)
        {
            failures.Add("Ai:Temperature must be between 0 and 1.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
