using System.Net.Http.Headers;

namespace SmartTask.Api.IntegrationTests;

/// <summary><c>POST ai/task-descriptions/improve</c> through the real pipeline, with the deterministic fake provider.</summary>
[Collection(ApiCollection.Name)]
public sealed class AiEndpointTests(SmartTaskApiFactory factory)
{
    private const string Url = "/api/v1/ai/task-descriptions/improve";
    private const string Password = "Str0ng!Pass";

    [Fact]
    public async Task Anonymous_Returns401()
    {
        var response = await factory.CreateApiClient().PostJsonAsync(Url, new { description = "fix login bug" });

        await response.ShouldBeEnvelopeAsync(401);
    }

    [Fact]
    public async Task SignedInTeamMember_GetsImprovedDescription()
    {
        var client = await SignedInClientAsync();

        var json = await (await client.PostJsonAsync(Url, new { title = "Login", description = "fix login bug" })).ShouldBeEnvelopeAsync(200);

        var data = json.GetProperty("data");
        data.GetProperty("originalDescription").GetString().ShouldBe("fix login bug");
        data.GetProperty("improvedDescription").GetString().ShouldNotBeNull().ShouldStartWith("Fix login bug.\n\nAcceptance criteria:");
        data.GetProperty("suggestions").GetArrayLength().ShouldBe(2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    public async Task TooShortDescription_Returns400(string description)
    {
        var client = await SignedInClientAsync();

        var json = await (await client.PostJsonAsync(Url, new { description })).ShouldBeEnvelopeAsync(400);

        json.GetProperty("errors").TryGetProperty("description", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task TooLongDescription_Returns400()
    {
        var client = await SignedInClientAsync();

        await (await client.PostJsonAsync(Url, new { description = new string('a', 2001) })).ShouldBeEnvelopeAsync(400);
    }

    [Fact]
    public async Task MoreThanTenCallsAMinute_AreRateLimitedPerUser()
    {
        var client = await SignedInClientAsync();

        for (var i = 0; i < 10; i++)
        {
            await (await client.PostJsonAsync(Url, new { description = "fix login bug" })).ShouldBeEnvelopeAsync(200);
        }

        var limited = await client.PostJsonAsync(Url, new { description = "fix login bug" });
        await limited.ShouldBeEnvelopeAsync(429);
        limited.Headers.RetryAfter.ShouldNotBeNull();

        // The bucket is per user: another user is not affected.
        var other = await SignedInClientAsync();
        await (await other.PostJsonAsync(Url, new { description = "fix login bug" })).ShouldBeEnvelopeAsync(200);
    }

    private async Task<HttpClient> SignedInClientAsync()
    {
        var client = factory.CreateApiClient();
        var email = $"ai-{Guid.NewGuid():N}@example.com";

        await (await client.PostJsonAsync("/api/v1/auth/register", new
        {
            firstName = "Ai",
            lastName = "Tester",
            email,
            password = Password,
            confirmPassword = Password,
        })).ShouldBeEnvelopeAsync(201);

        var login = await (await client.PostJsonAsync("/api/v1/auth/login", new { email, password = Password })).ShouldBeEnvelopeAsync(200);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.GetProperty("data").GetProperty("accessToken").GetString());
        return client;
    }
}
