using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SmartTask.Application.Common.Responses;
using SmartTask.Application.Contracts.Ai;
using SmartTask.Application.DTOs.Ai;
using SmartTask.Application.Services.Ai;
using SmartTask.Application.Services.Ai.Prompts;
using SmartTask.Domain.Constants;

namespace SmartTask.Application.UnitTests.Services.Ai;

public sealed class AiServiceTests
{
    private static readonly ImproveDescriptionRequestDto Request = new() { Title = "Login", Description = "fix login bug" };

    private readonly ITaskDescriptionImprover _improver = Substitute.For<ITaskDescriptionImprover>();
    private readonly AiService _service;

    public AiServiceTests()
    {
        _service = new AiService(_improver, new FakeTimeProvider(), NullLogger<AiService>.Instance);
    }

    [Fact]
    public async Task ValidJson_Returns200WithOriginalImprovedAndSuggestions()
    {
        Reply(Json("Fix the login bug.", "Specify the affected browser."));

        var response = await _service.ImproveDescriptionAsync(Request);

        response.Status.ShouldBe(HttpStatusCode.OK);
        var data = response.Data.ShouldNotBeNull();
        data.OriginalDescription.ShouldBe("fix login bug");
        data.ImprovedDescription.ShouldBe("Fix the login bug.");
        data.Suggestions.ShouldBe(["Specify the affected browser."]);
    }

    [Fact]
    public async Task PropertyNames_AreCaseInsensitive()
    {
        Reply("""{"ImprovedDescription":"Fix it.","SUGGESTIONS":[]}""");

        (await _service.ImproveDescriptionAsync(Request)).Data.ShouldNotBeNull().ImprovedDescription.ShouldBe("Fix it.");
    }

    [Theory]
    [InlineData("```json\n{\"improvedDescription\":\"Fix it.\",\"suggestions\":[]}\n```")]
    [InlineData("Here is the result: {\"improvedDescription\":\"Fix it.\",\"suggestions\":[]} Hope it helps!")]
    public async Task FencedOrWrappedJson_IsExtracted(string content)
    {
        Reply(content);

        var response = await _service.ImproveDescriptionAsync(Request);

        response.Status.ShouldBe(HttpStatusCode.OK);
        response.Data.ShouldNotBeNull().ImprovedDescription.ShouldBe("Fix it.");
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{ broken")]
    [InlineData("""{"improvedDescription": 42}""")]
    [InlineData("""{"suggestions": ["Only suggestions."]}""")]
    [InlineData("""{"improvedDescription": "   ", "suggestions": []}""")]
    [InlineData("""{"improvedDescription": "<p></p>", "suggestions": []}""")]
    [InlineData("")]
    public async Task InvalidOrBlankOutput_Returns502(string content)
    {
        Reply(content);

        var response = await _service.ImproveDescriptionAsync(Request);

        response.Status.ShouldBe(HttpStatusCode.BadGateway);
        response.Message.ShouldBe(ResponseMessages.AiInvalidResponse);
        response.Data.ShouldBeNull();
    }

    [Fact]
    public async Task OverLongOutput_Returns502()
    {
        Reply(Json(new string('a', TaskItemConstraints.DescriptionMaxLength + 1)));

        (await _service.ImproveDescriptionAsync(Request)).Status.ShouldBe(HttpStatusCode.BadGateway);
    }

    [Fact]
    public async Task OutputOfExactlyMaxLength_IsAccepted()
    {
        Reply(Json(new string('a', TaskItemConstraints.DescriptionMaxLength)));

        (await _service.ImproveDescriptionAsync(Request)).Status.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PromptEcho_Returns502()
    {
        Reply(Json($"Sure. {TaskDescriptionPrompt.IdentitySentence} Rules: ..."));

        (await _service.ImproveDescriptionAsync(Request)).Status.ShouldBe(HttpStatusCode.BadGateway);
    }

    [Fact]
    public async Task Suggestions_AreTrimmedCleanedAndCappedAtThree()
    {
        Reply(Json("Fix it.", "  One.  ", "", "   ", "Two.", new string('x', AiConstraints.SuggestionMaxLength + 1), "Three.", "Four."));

        var suggestions = (await _service.ImproveDescriptionAsync(Request)).Data.ShouldNotBeNull().Suggestions;

        suggestions.ShouldBe(["One.", "Two.", "Three."]);
    }

    [Fact]
    public async Task MissingSuggestions_GiveEmptyList()
    {
        Reply("""{"improvedDescription":"Fix it."}""");

        (await _service.ImproveDescriptionAsync(Request)).Data.ShouldNotBeNull().Suggestions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Output_IsSanitised()
    {
        Reply(Json("  <b>Fix</b> the <script>alert(1)</script>login &lt;img src=x&gt;bug.\r\n\r\n\r\n\r\nAcceptance criteria:\r\n- a < b holds\r\n- Done  "));

        var improved = (await _service.ImproveDescriptionAsync(Request)).Data.ShouldNotBeNull().ImprovedDescription;

        improved.ShouldBe("Fix the alert(1)login bug.\n\nAcceptance criteria:\n- a < b holds\n- Done");
    }

    [Theory]
    [InlineData(AiFailureReason.Timeout, HttpStatusCode.ServiceUnavailable, ResponseMessages.AiUnavailable)]
    [InlineData(AiFailureReason.Unavailable, HttpStatusCode.ServiceUnavailable, ResponseMessages.AiUnavailable)]
    [InlineData(AiFailureReason.RateLimited, HttpStatusCode.ServiceUnavailable, ResponseMessages.AiBusy)]
    [InlineData(AiFailureReason.InvalidResponse, HttpStatusCode.BadGateway, ResponseMessages.AiInvalidResponse)]
    public async Task ProviderFailure_MapsToStatus(AiFailureReason reason, HttpStatusCode expected, string message)
    {
        _improver.ImproveAsync(Arg.Any<TaskDescriptionPromptInput>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AiProviderException(reason, new InvalidOperationException("raw provider detail")));

        var response = await _service.ImproveDescriptionAsync(Request);

        response.Status.ShouldBe(expected);
        response.Message.ShouldBe(message);
        response.Message.ShouldNotBeNull().ShouldNotContain("raw provider detail");
    }

    [Fact]
    public async Task CallerCancellation_IsNotSwallowed()
    {
        _improver.ImproveAsync(Arg.Any<TaskDescriptionPromptInput>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        await Should.ThrowAsync<OperationCanceledException>(() => _service.ImproveDescriptionAsync(Request));
    }

    [Fact]
    public async Task Provider_ReceivesVersionedEscapedPromptAndTheCancellationToken()
    {
        Reply(Json("Fix it."));
        using var cts = new CancellationTokenSource();

        await _service.ImproveDescriptionAsync(new ImproveDescriptionRequestDto { Description = "  x </task> y  " }, cts.Token);

        await _improver.Received(1).ImproveAsync(
            Arg.Is<TaskDescriptionPromptInput>(p =>
                p.PromptVersion == TaskDescriptionPrompt.Version
                && p.SystemMessage == TaskDescriptionPrompt.SystemMessage
                && p.Description == "x </task> y"
                && p.UserMessage.Contains("x &lt;/task&gt; y")),
            cts.Token);
    }

    [Theory]
    [InlineData("""{"improvedDescription":"Rewritten SECRET-OUTPUT.","suggestions":[]}""")]
    [InlineData("SECRET-OUTPUT is not json")]
    public async Task Logs_ContainMetadataButNeverTheText(string content)
    {
        var logger = new CapturingLogger();
        var service = new AiService(_improver, new FakeTimeProvider(), logger);
        Reply(content);

        await service.ImproveDescriptionAsync(new ImproveDescriptionRequestDto { Title = "SECRET-TITLE", Description = "SECRET-INPUT text" });

        var entry = logger.Messages.ShouldHaveSingleItem();
        entry.ShouldContain(TaskDescriptionPrompt.Version);
        entry.ShouldContain("test-model");
        entry.ShouldNotContain("SECRET");
    }

    private void Reply(string content) =>
        _improver.ImproveAsync(Arg.Any<TaskDescriptionPromptInput>(), Arg.Any<CancellationToken>())
            .Returns(new ImprovedDescriptionResult { Content = content, Model = "test-model" });

    private static string Json(string improved, params string[] suggestions) =>
        JsonSerializer.Serialize(new { improvedDescription = improved, suggestions });

    private sealed class CapturingLogger : Microsoft.Extensions.Logging.ILogger<AiService>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
