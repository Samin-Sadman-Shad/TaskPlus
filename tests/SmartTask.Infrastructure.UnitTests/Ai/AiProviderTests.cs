using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartTask.Application.Contracts.Ai;
using SmartTask.Application.Services.Ai.Prompts;
using SmartTask.Infrastructure.Ai;

namespace SmartTask.Infrastructure.UnitTests.Ai;

public sealed class FakeTaskDescriptionImproverTests
{
    private readonly FakeTaskDescriptionImprover _fake = new();

    [Fact]
    public async Task SameInput_GivesSameOutput()
    {
        var input = TaskDescriptionPrompt.Build("Login", "fix login bug");

        var first = await _fake.ImproveAsync(input);
        var second = await _fake.ImproveAsync(input);

        second.ShouldBe(first);
        first.Model.ShouldBe(FakeTaskDescriptionImprover.ModelName);
    }

    [Fact]
    public async Task ReturnsTheJsonContractWithAnImprovedSentence()
    {
        var result = await _fake.ImproveAsync(TaskDescriptionPrompt.Build(null, "  fix login bug  "));

        using var json = JsonDocument.Parse(result.Content);
        var improved = json.RootElement.GetProperty("improvedDescription").GetString().ShouldNotBeNull();
        improved.ShouldStartWith("Fix login bug.\n\nAcceptance criteria:\n- ");
        json.RootElement.GetProperty("suggestions").EnumerateArray().Select(s => s.GetString())
            .ShouldBe(FakeTaskDescriptionImprover.Suggestions);
    }

    [Fact]
    public async Task UsesTheRawDescription_NotTheEscapedPrompt()
    {
        var result = await _fake.ImproveAsync(TaskDescriptionPrompt.Build(null, "check a < b."));

        JsonDocument.Parse(result.Content).RootElement.GetProperty("improvedDescription").GetString()
            .ShouldNotBeNull().ShouldStartWith("Check a < b.\n");
    }
}

public sealed class AiOptionsValidatorTests
{
    private readonly AiOptionsValidator _validator = new();

    [Fact]
    public void Defaults_AreValid_EvenWithoutKey() =>
        _validator.Validate(null, new AiOptions()).Succeeded.ShouldBeTrue();

    [Theory]
    [InlineData("http://models.github.ai/inference")]
    [InlineData("models.github.ai/inference")]
    [InlineData("")]
    public void Endpoint_MustBeAbsoluteHttps(string endpoint) =>
        Failure(new AiOptions { Endpoint = endpoint }).ShouldContain("Ai:Endpoint");

    [Theory]
    [InlineData(4)]
    [InlineData(61)]
    public void TimeoutSeconds_OutOfRange_Fails(int seconds) =>
        Failure(new AiOptions { TimeoutSeconds = seconds }).ShouldContain("Ai:TimeoutSeconds");

    [Theory]
    [InlineData(5)]
    [InlineData(60)]
    public void TimeoutSeconds_Bounds_AreValid(int seconds) =>
        _validator.Validate(null, new AiOptions { TimeoutSeconds = seconds }).Succeeded.ShouldBeTrue();

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(1.1f)]
    [InlineData(float.NaN)]
    public void Temperature_OutOfRange_Fails(float temperature) =>
        Failure(new AiOptions { Temperature = temperature }).ShouldContain("Ai:Temperature");

    [Fact]
    public void MissingModel_Fails() => Failure(new AiOptions { Model = " " }).ShouldContain("Ai:Model");

    [Fact]
    public void MaxOutputTokens_OutOfRange_Fails() => Failure(new AiOptions { MaxOutputTokens = 0 }).ShouldContain("Ai:MaxOutputTokens");

    [Fact]
    public void Options_ToString_NeverShowsTheKey() =>
        new AiOptions { ApiKey = "pat-secret-value-123" }.ToString().ShouldNotContain("pat-secret-value-123");

    private string Failure(AiOptions options)
    {
        var result = _validator.Validate(null, options);
        result.Failed.ShouldBeTrue();
        return result.FailureMessage.ShouldNotBeNull();
    }
}

public sealed class AiProviderRegistrationTests
{
    private const string JwtKey = "unit-test-signing-key-0123456789-abcdefghijklmnopqrstuvwxyz";

    [Fact]
    public void GitHubModelsWithoutKey_FallsBackToFake()
    {
        using var provider = Build(new() { ["Ai:Provider"] = "GitHubModels" });

        provider.GetRequiredService<ITaskDescriptionImprover>().ShouldBeOfType<FakeTaskDescriptionImprover>();
        provider.GetRequiredService<AiProviderSelection>().FellBackToFake.ShouldBeTrue();
    }

    [Fact]
    public void NoAiSection_FallsBackToFake()
    {
        using var provider = Build([]);

        provider.GetRequiredService<ITaskDescriptionImprover>().ShouldBeOfType<FakeTaskDescriptionImprover>();
    }

    [Fact]
    public void FakeConfigured_UsesFake_WithoutFallbackWarning()
    {
        using var provider = Build(new() { ["Ai:Provider"] = "Fake", ["Ai:ApiKey"] = "some-key" });

        provider.GetRequiredService<ITaskDescriptionImprover>().ShouldBeOfType<FakeTaskDescriptionImprover>();
        provider.GetRequiredService<AiProviderSelection>().FellBackToFake.ShouldBeFalse();
    }

    [Fact]
    public void GitHubModelsWithKey_UsesGitHubModels()
    {
        using var provider = Build(new() { ["Ai:Provider"] = "GitHubModels", ["Ai:ApiKey"] = "some-key" });

        provider.GetRequiredService<ITaskDescriptionImprover>().ShouldBeOfType<GitHubModelsTaskDescriptionImprover>();
    }

    [Fact]
    public void InvalidOptions_FailValidation()
    {
        using var provider = Build(new() { ["Ai:TimeoutSeconds"] = "0" });

        Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<AiOptions>>().Value);
    }

    [Fact]
    public async Task Startup_LogsWarningWhenFallingBack_WithoutTheKey()
    {
        var logs = new ListLoggerProvider();
        using var provider = Build(new() { ["Ai:Provider"] = "GitHubModels" }, logs);

        foreach (var service in provider.GetServices<IHostedService>())
        {
            await service.StartAsync(CancellationToken.None);
        }

        logs.Entries.ShouldContain(e => e.Level == LogLevel.Warning && e.Message.Contains("using the fake AI provider"));
    }

    private static ServiceProvider Build(Dictionary<string, string?> settings, ILoggerProvider? logs = null)
    {
        settings["Jwt:Issuer"] = "test";
        settings["Jwt:Audience"] = "test";
        settings["Jwt:SigningKey"] = JwtKey;

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            if (logs is not null)
            {
                builder.AddProvider(logs);
            }
        });
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private sealed class ListLoggerProvider : ILoggerProvider
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new ListLogger(Entries);

        public void Dispose()
        {
        }

        private sealed class ListLogger(List<(LogLevel, string)> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
