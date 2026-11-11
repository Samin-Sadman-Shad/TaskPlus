using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmartTask.Application.Common.Responses;
using SmartTask.Application.Contracts.Ai;
using SmartTask.Application.Contracts.Services;
using SmartTask.Application.DTOs.Ai;
using SmartTask.Application.Services.Ai.Prompts;
using SmartTask.Domain.Constants;

namespace SmartTask.Application.Services.Ai;

/// <summary>
/// Orchestrates one improvement: build the prompt, call the provider, then treat the reply as untrusted
/// input (parse, validate, sanitise) before anything reaches the client. Nothing is saved.
/// Logs only metadata (prompt version, model, latency, lengths), never the text itself.
/// </summary>
public sealed partial class AiService(
    ITaskDescriptionImprover improver,
    TimeProvider time,
    ILogger<AiService> logger) : IAiService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<BaseResponse<ImproveDescriptionResponseDto>> ImproveDescriptionAsync(
        ImproveDescriptionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var prompt = TaskDescriptionPrompt.Build(request.Title, request.Description);
        var started = time.GetTimestamp();

        ImprovedDescriptionResult result;
        try
        {
            result = await improver.ImproveAsync(prompt, cancellationToken);
        }
        catch (AiProviderException ex)
        {
            LogProviderFailed(ex.Reason, prompt.PromptVersion, time.GetElapsedTime(started).TotalMilliseconds, prompt.Description.Length);
            return ProviderFailure(ex.Reason);
        }

        var latencyMs = time.GetElapsedTime(started).TotalMilliseconds;

        if (!TryReadOutput(result.Content, out var output, out var rejection))
        {
            LogOutputRejected(rejection, prompt.PromptVersion, result.Model, latencyMs, prompt.Description.Length, result.Content.Length);
            return BaseResponse<ImproveDescriptionResponseDto>.Fail(HttpStatusCode.BadGateway, ResponseMessages.AiInvalidResponse);
        }

        LogImproved(prompt.PromptVersion, result.Model, latencyMs, prompt.Description.Length, output.ImprovedDescription.Length, output.Suggestions.Count);

        return BaseResponse<ImproveDescriptionResponseDto>.Ok(new ImproveDescriptionResponseDto
        {
            OriginalDescription = request.Description,
            ImprovedDescription = output.ImprovedDescription,
            Suggestions = output.Suggestions,
        });
    }

    private static BaseResponse<ImproveDescriptionResponseDto> ProviderFailure(AiFailureReason reason) => reason switch
    {
        AiFailureReason.RateLimited => BaseResponse<ImproveDescriptionResponseDto>.Fail(HttpStatusCode.ServiceUnavailable, ResponseMessages.AiBusy),
        AiFailureReason.InvalidResponse => BaseResponse<ImproveDescriptionResponseDto>.Fail(HttpStatusCode.BadGateway, ResponseMessages.AiInvalidResponse),
        _ => BaseResponse<ImproveDescriptionResponseDto>.Fail(HttpStatusCode.ServiceUnavailable, ResponseMessages.AiUnavailable),
    };

    /// <summary>Parses, validates and sanitises the model output. <paramref name="rejection"/> is a fixed code for the log, never model text.</summary>
    private static bool TryReadOutput(string content, out SanitizedOutput output, out string rejection)
    {
        output = default!;

        if (ParseOutput(content) is not { } raw)
        {
            rejection = "not-json";
            return false;
        }

        var improved = AiOutputSanitizer.Sanitize(raw.ImprovedDescription);

        if (string.IsNullOrWhiteSpace(improved))
        {
            rejection = "blank";
            return false;
        }

        if (improved.Length > TaskItemConstraints.DescriptionMaxLength)
        {
            rejection = "too-long";
            return false;
        }

        if (improved.Contains(TaskDescriptionPrompt.IdentitySentence, StringComparison.OrdinalIgnoreCase))
        {
            rejection = "prompt-echo";
            return false;
        }

        rejection = string.Empty;
        output = new SanitizedOutput(improved, CleanSuggestions(raw.Suggestions));
        return true;
    }

    /// <summary>Parses the whole reply; if that fails, once more with the first <c>{</c> to the last <c>}</c> (handles Markdown fences or chatter around the JSON).</summary>
    private static AiOutput? ParseOutput(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        if (TryDeserialize(content) is { } direct)
        {
            return direct;
        }

        var start = content.IndexOf('{', StringComparison.Ordinal);
        var end = content.LastIndexOf('}');
        return start >= 0 && end > start ? TryDeserialize(content[start..(end + 1)]) : null;
    }

    private static AiOutput? TryDeserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<AiOutput>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static List<string> CleanSuggestions(IEnumerable<string?>? suggestions) =>
        (suggestions ?? [])
            .Select(s => AiOutputSanitizer.Sanitize(s))
            .Where(s => s.Length > 0 && s.Length <= AiConstraints.SuggestionMaxLength)
            .Take(AiConstraints.MaxSuggestions)
            .ToList();

    /// <summary>The JSON contract the prompt asks for. Fields are nullable because the model may omit them.</summary>
    private sealed record AiOutput(string? ImprovedDescription, List<string?>? Suggestions);

    private sealed record SanitizedOutput(string ImprovedDescription, IReadOnlyList<string> Suggestions);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "AI description improved (prompt {PromptVersion}, model {Model}) in {LatencyMs:F0} ms; input {InputLength} chars, output {OutputLength} chars, {SuggestionCount} suggestions")]
    private partial void LogImproved(string promptVersion, string model, double latencyMs, int inputLength, int outputLength, int suggestionCount);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "AI output rejected ({Rejection}) (prompt {PromptVersion}, model {Model}) after {LatencyMs:F0} ms; input {InputLength} chars, output {OutputLength} chars")]
    private partial void LogOutputRejected(string rejection, string promptVersion, string model, double latencyMs, int inputLength, int outputLength);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "AI provider failed ({Reason}) (prompt {PromptVersion}) after {LatencyMs:F0} ms; input {InputLength} chars")]
    private partial void LogProviderFailed(AiFailureReason reason, string promptVersion, double latencyMs, int inputLength);
}
