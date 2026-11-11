using System.Text.Json;
using SmartTask.Application.Contracts.Ai;

namespace SmartTask.Infrastructure.Ai;

/// <summary>
/// Offline provider with deterministic output: the same input always gives the same reply. Registered
/// when no API key is configured (so the endpoint works on a fresh clone) and used by the integration
/// tests. It returns the same JSON shape as a real model, so the reply goes through all of
/// <c>AiService</c>'s parsing and validation.
/// </summary>
public sealed class FakeTaskDescriptionImprover : ITaskDescriptionImprover
{
    public const string ModelName = "fake";

    public static readonly IReadOnlyList<string> Suggestions =
    [
        "Specify the target completion date.",
        "Describe how the result will be verified.",
    ];

    public Task<ImprovedDescriptionResult> ImproveAsync(TaskDescriptionPromptInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();

        var improved =
            $"{ToSentence(input.Description)}\n\n" +
            "Acceptance criteria:\n" +
            "- The work described above is complete.\n" +
            "- The result has been reviewed and verified.";

        var content = JsonSerializer.Serialize(new { improvedDescription = improved, suggestions = Suggestions });
        return Task.FromResult(new ImprovedDescriptionResult { Content = content, Model = ModelName });
    }

    /// <summary>Trims, capitalises the first letter and ends with a full stop.</summary>
    private static string ToSentence(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return trimmed;
        }

        var sentence = char.ToUpperInvariant(trimmed[0]) + trimmed[1..];
        return sentence[^1] is '.' or '!' or '?' ? sentence : sentence + ".";
    }
}
