namespace SmartTask.Application.Contracts.Ai;

/// <summary>The provider's raw reply. It is untrusted until <c>AiService</c> has parsed and validated it.</summary>
public sealed record ImprovedDescriptionResult
{
    /// <summary>The model's text; expected to be the JSON object the prompt asks for.</summary>
    public required string Content { get; init; }

    /// <summary>The model that answered (for logging).</summary>
    public required string Model { get; init; }

    /// <summary>Never prints the content, so the record can't leak model output into a log by accident.</summary>
    public override string ToString() =>
        $"{nameof(ImprovedDescriptionResult)} {{ Model = {Model}, ContentLength = {Content.Length} }}";
}
