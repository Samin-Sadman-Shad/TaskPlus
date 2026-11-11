namespace SmartTask.Domain.Constants;

public static class AiConstraints
{
    public const int InputMinLength = 3;
    public const int InputMaxLength = 2000;

    /// <summary>Length the prompt asks the model to stay under. The hard output limit is <see cref="TaskItemConstraints.DescriptionMaxLength"/>.</summary>
    public const int OutputTargetLength = 1200;

    /// <summary>Most suggestions returned to the client; extra ones from the model are dropped.</summary>
    public const int MaxSuggestions = 3;

    /// <summary>Longest suggestion kept; longer ones are dropped.</summary>
    public const int SuggestionMaxLength = 200;
}
