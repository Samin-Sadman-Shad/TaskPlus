using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using SmartTask.Application.Contracts.Ai;

namespace SmartTask.Infrastructure.Ai;

/// <summary>
/// Calls GitHub Models through <see cref="IChatClient"/> (Microsoft.Extensions.AI over the official OpenAI
/// SDK). Any OpenAI-compatible endpoint works by changing <c>Ai:Endpoint</c> and <c>Ai:Model</c>.
/// </summary>
/// <remarks>
/// Resilience: the OpenAI SDK pipeline retries 408/429/5xx with exponential backoff and honours
/// <c>Retry-After</c> (<see cref="CreateChatClient"/> sets <see cref="MaxRetries"/>). A linked
/// <see cref="CancellationTokenSource"/> caps the whole call, retries included, at <c>Ai:TimeoutSeconds</c>.
/// Every failure becomes an <see cref="AiProviderException"/>; provider error text is never passed on.
/// </remarks>
public sealed partial class GitHubModelsTaskDescriptionImprover(
    [FromKeyedServices(GitHubModelsTaskDescriptionImprover.ChatClientKey)] IChatClient chatClient,
    IOptions<AiOptions> options,
    TimeProvider time,
    ILogger<GitHubModelsTaskDescriptionImprover> logger) : ITaskDescriptionImprover
{
    /// <summary>DI key of the chat client, so it can't clash with another <see cref="IChatClient"/> registration.</summary>
    public const string ChatClientKey = "SmartTask.Ai.GitHubModels";

    public const int MaxRetries = 2;

    private const int TooManyRequests = 429;

    private readonly AiOptions _options = options.Value;

    /// <summary>Builds the chat client for <c>Ai:Endpoint</c> / <c>Ai:Model</c>, authenticated with <c>Ai:ApiKey</c>.</summary>
    public static IChatClient CreateChatClient(AiOptions options) => CreateChatClient(options, transport: null);

    /// <summary>Tests pass a transport over a stub <see cref="HttpMessageHandler"/> to see the real wire format.</summary>
    internal static IChatClient CreateChatClient(AiOptions options, PipelineTransport? transport)
    {
        ArgumentNullException.ThrowIfNull(options);

        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri(options.Endpoint),
            NetworkTimeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
            RetryPolicy = new ClientRetryPolicy(MaxRetries),
            UserAgentApplicationId = "SmartTask",
        };

        if (transport is not null)
        {
            clientOptions.Transport = transport;
        }

        return new OpenAIClient(new ApiKeyCredential(options.ApiKey), clientOptions)
            .GetChatClient(options.Model)
            .AsIChatClient();
    }

    public async Task<ImprovedDescriptionResult> ImproveAsync(TaskDescriptionPromptInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        ChatMessage[] messages =
        [
            new(ChatRole.System, input.SystemMessage),
            new(ChatRole.User, input.UserMessage),
        ];

        var chatOptions = new ChatOptions
        {
            ModelId = _options.Model,
            Temperature = _options.Temperature,
            MaxOutputTokens = _options.MaxOutputTokens,
            ResponseFormat = ChatResponseFormat.Json,
        };

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds), time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        ChatResponse response;
        try
        {
            response = await chatClient.GetResponseAsync(messages, chatOptions, linked.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // the caller gave up (client disconnected): not a provider failure
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested)
        {
            // Same, when the SDK wrapped the cancellation (e.g. in an AggregateException after retries).
            throw new OperationCanceledException("The request was cancelled.", ex, cancellationToken);
        }
        catch (Exception ex) when (Classify(ex) is { } reason)
        {
            if (FindStatus(ex) is { } status)
            {
                LogProviderStatus(status, _options.Model);
            }

            throw new AiProviderException(reason, ex);
        }

        // A cut-off reply (token limit) or a filtered one is not valid JSON we can trust.
        if (response.FinishReason == ChatFinishReason.Length || response.FinishReason == ChatFinishReason.ContentFilter
            || string.IsNullOrWhiteSpace(response.Text))
        {
            LogUnusableReply(response.FinishReason?.Value ?? "none", _options.Model);
            throw new AiProviderException(AiFailureReason.InvalidResponse);
        }

        return new ImprovedDescriptionResult
        {
            Content = response.Text,
            Model = response.ModelId ?? _options.Model,
        };
    }

    /// <summary>
    /// Maps a failure to a reason, or <c>null</c> for exceptions that are bugs (they reach the global
    /// handler as 500). When retries run out after network errors, the SDK throws an
    /// <see cref="AggregateException"/> of the attempts; the last attempt decides.
    /// </summary>
    private static AiFailureReason? Classify(Exception ex) => ex switch
    {
        AggregateException { InnerExceptions.Count: > 0 } aggregate => Classify(aggregate.InnerExceptions[^1]),
        OperationCanceledException => AiFailureReason.Timeout, // ours or the SDK's network timeout; caller cancellation is handled above
        ClientResultException { Status: TooManyRequests } => AiFailureReason.RateLimited,
        ClientResultException or HttpRequestException or IOException => AiFailureReason.Unavailable,
        _ => null,
    };

    private static int? FindStatus(Exception ex) => ex switch
    {
        AggregateException { InnerExceptions.Count: > 0 } aggregate => FindStatus(aggregate.InnerExceptions[^1]),
        ClientResultException { Status: > 0 } result => result.Status,
        _ => null,
    };

    /// <summary>Logs the status only. 401/403 usually mean a wrong or expired token, or a missing <c>models:read</c> permission.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "AI provider returned HTTP {Status} for model {Model}")]
    private partial void LogProviderStatus(int status, string model);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI provider reply unusable (finish reason {FinishReason}) for model {Model}")]
    private partial void LogUnusableReply(string finishReason, string model);
}
