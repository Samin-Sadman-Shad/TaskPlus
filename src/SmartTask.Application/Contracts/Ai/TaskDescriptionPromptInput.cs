namespace SmartTask.Application.Contracts.Ai;

/// <summary>
/// The prompt for one call, built by <c>TaskDescriptionPrompt</c>. <see cref="Title"/> and
/// <see cref="Description"/> are the raw user input (for providers that don't call a model, such as the
/// fake); real providers send only <see cref="SystemMessage"/> and <see cref="UserMessage"/>.
/// </summary>
public sealed record TaskDescriptionPromptInput
{
    public required string PromptVersion { get; init; }

    public required string SystemMessage { get; init; }

    /// <summary>The user input inside the <c>&lt;task&gt;</c> delimiters, already escaped.</summary>
    public required string UserMessage { get; init; }

    public string? Title { get; init; }

    public required string Description { get; init; }

    /// <summary>Never prints the user's text, so the record can't leak it into a log by accident.</summary>
    public override string ToString() =>
        $"{nameof(TaskDescriptionPromptInput)} {{ PromptVersion = {PromptVersion}, DescriptionLength = {Description.Length} }}";
}
