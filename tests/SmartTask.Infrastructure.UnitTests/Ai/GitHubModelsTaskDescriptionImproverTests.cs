using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using SmartTask.Application.Contracts.Ai;
using SmartTask.Application.Services.Ai.Prompts;
using SmartTask.Infrastructure.Ai;

namespace SmartTask.Infrastructure.UnitTests.Ai;

/// <summary>
/// Runs the real OpenAI SDK pipeline against a stub HTTP handler, so these tests see the actual request
/// on the wire and the SDK's retry behaviour, without a network or a key.
/// </summary>
public sealed class GitHubModelsTaskDescriptionImproverTests
{
    private const string TestKey = "test-key-not-real";
    private const string ModelReply = """{"improvedDescription":"Fix the login bug.","suggestions":[]}""";

    private static readonly TaskDescriptionPromptInput Input = TaskDescriptionPrompt.Build("Login", "fix login bug");

    [Fact]
    public async Task SendsChatCompletionWithPromptOptionsAndKey()
    {
        var handler = new StubHandler(_ => Completion(ModelReply));

        var result = await Create(handler).ImproveAsync(Input);

        result.Content.ShouldBe(ModelReply);
        result.Model.ShouldBe("openai/gpt-4.1-mini");

        var request = handler.Requests.ShouldHaveSingleItem();
        request.Uri.ShouldBe("https://models.github.ai/inference/chat/completions");
        request.Authorization.ShouldBe($"Bearer {TestKey}");

        using var body = JsonDocument.Parse(request.Body);
        var root = body.RootElement;
        root.GetProperty("model").GetString().ShouldBe("openai/gpt-4.1-mini");
        root.GetProperty("temperature").GetDouble().ShouldBe(0.3, tolerance: 0.0001);
        TokenLimit(root).ShouldBe(600);
        root.GetProperty("response_format").GetProperty("type").GetString().ShouldBe("json_object");

        var messages = root.GetProperty("messages");
        messages.GetArrayLength().ShouldBe(2);
        messages[0].GetProperty("role").GetString().ShouldBe("system");
        messages[0].GetProperty("content").GetString().ShouldBe(TaskDescriptionPrompt.SystemMessage);
        messages[1].GetProperty("role").GetString().ShouldBe("user");
        messages[1].GetProperty("content").GetString().ShouldBe(Input.UserMessage);
    }

    [Fact]
    public async Task RateLimited_IsRetriedThenMapsToRateLimited()
    {
        var handler = new StubHandler(_ => TooManyRequests());

        var ex = await Should.ThrowAsync<AiProviderException>(() => Create(handler).ImproveAsync(Input));

        ex.Reason.ShouldBe(AiFailureReason.RateLimited);
        handler.Requests.Count.ShouldBe(1 + GitHubModelsTaskDescriptionImprover.MaxRetries);
    }

    [Fact]
    public async Task TransientFailure_IsRetriedAndThenSucceeds()
    {
        var calls = 0;
        var handler = new StubHandler(_ => ++calls == 1 ? TooManyRequests() : Completion(ModelReply));

        var result = await Create(handler).ImproveAsync(Input);

        result.Content.ShouldBe(ModelReply);
        handler.Requests.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task ErrorStatus_MapsToUnavailable_WithoutLeakingProviderText(HttpStatusCode status)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent("""{"error":{"message":"provider secret detail"}}""", Encoding.UTF8, "application/json"),
            Headers = { { "retry-after-ms", "1" } },
        });

        var ex = await Should.ThrowAsync<AiProviderException>(() => Create(handler).ImproveAsync(Input));

        ex.Reason.ShouldBe(AiFailureReason.Unavailable);
        ex.Message.ShouldNotContain("provider secret detail");
    }

    [Fact]
    public async Task NetworkError_MapsToUnavailable()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("connection refused"));

        var ex = await Should.ThrowAsync<AiProviderException>(() => Create(handler).ImproveAsync(Input));

        ex.Reason.ShouldBe(AiFailureReason.Unavailable);
    }

    [Theory]
    [InlineData("", "stop")]
    [InlineData("""{"improvedDescription":"cut o""", "length")]
    [InlineData("filtered", "content_filter")]
    public async Task EmptyTruncatedOrFilteredReply_MapsToInvalidResponse(string content, string finishReason)
    {
        var handler = new StubHandler(_ => Completion(content, finishReason));

        var ex = await Should.ThrowAsync<AiProviderException>(() => Create(handler).ImproveAsync(Input));

        ex.Reason.ShouldBe(AiFailureReason.InvalidResponse);
    }

    [Fact]
    public async Task NoAnswerWithinTimeout_MapsToTimeout()
    {
        var time = new FakeTimeProvider();
        var improver = Create(new HangingChatClient(), time);

        var call = improver.ImproveAsync(Input);
        time.Advance(TimeSpan.FromSeconds(new AiOptions().TimeoutSeconds + 1));

        (await Should.ThrowAsync<AiProviderException>(() => call)).Reason.ShouldBe(AiFailureReason.Timeout);
    }

    [Fact]
    public async Task CallerCancellation_PropagatesAsCancellation()
    {
        using var cts = new CancellationTokenSource();
        var call = Create(new HangingChatClient(), new FakeTimeProvider()).ImproveAsync(Input, cts.Token);

        await cts.CancelAsync();

        var ex = await Should.ThrowAsync<OperationCanceledException>(() => call);
        ex.ShouldNotBeOfType<AiProviderException>();
    }

    private static GitHubModelsTaskDescriptionImprover Create(StubHandler handler)
    {
        var options = new AiOptions { ApiKey = TestKey };
        var chatClient = GitHubModelsTaskDescriptionImprover.CreateChatClient(options, new HttpClientPipelineTransport(new HttpClient(handler)));
        return Create(chatClient, TimeProvider.System, options);
    }

    private static GitHubModelsTaskDescriptionImprover Create(IChatClient chatClient, TimeProvider time, AiOptions? options = null) =>
        new(chatClient, Options.Create(options ?? new AiOptions { ApiKey = TestKey }), time,
            NullLogger<GitHubModelsTaskDescriptionImprover>.Instance);

    /// <summary>Newer SDKs send <c>max_completion_tokens</c>, older ones <c>max_tokens</c>.</summary>
    private static int TokenLimit(JsonElement root) =>
        root.TryGetProperty("max_completion_tokens", out var limit) || root.TryGetProperty("max_tokens", out limit)
            ? limit.GetInt32()
            : throw new ShouldAssertException("No output-token limit in the request.");

    private static HttpResponseMessage Completion(string content, string finishReason = "stop")
    {
        var json = JsonSerializer.Serialize(new
        {
            id = "chatcmpl-test",
            @object = "chat.completion",
            created = 1_700_000_000,
            model = "openai/gpt-4.1-mini",
            choices = new[]
            {
                new { index = 0, message = new { role = "assistant", content }, finish_reason = finishReason },
            },
            usage = new { prompt_tokens = 10, completion_tokens = 10, total_tokens = 20 },
        });

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    /// <summary><c>retry-after-ms</c> keeps the SDK's backoff short in tests.</summary>
    private static HttpResponseMessage TooManyRequests() => new(HttpStatusCode.TooManyRequests)
    {
        Content = new StringContent("""{"error":{"message":"rate limited"}}""", Encoding.UTF8, "application/json"),
        Headers = { { "retry-after-ms", "1" } },
    };

    private sealed record CapturedRequest(string Uri, string? Authorization, string Body);

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new CapturedRequest(request.RequestUri!.ToString(), request.Headers.Authorization?.ToString(), body));
            return respond(request);
        }
    }

    /// <summary>Never answers; only cancellation ends the call.</summary>
    private sealed class HangingChatClient : IChatClient
    {
        public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("unreachable");
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
