using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace SmartTask.Api.IntegrationTests;

/// <summary>Cross-cutting behaviour of the hosted pipeline: health, error envelope, security headers, CORS and Swagger.</summary>
[Collection(ApiCollection.Name)]
public sealed class PipelineTests(SmartTaskApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateApiClient();

    [Fact]
    public async Task HealthLive_IsAnonymousAndHealthy()
    {
        var response = await _client.GetAsync("/health/live");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ApiJson.ReadAsync(response)).GetProperty("status").GetString().ShouldBe("Healthy");
    }

    [Fact]
    public async Task HealthReady_ChecksTheDatabase()
    {
        var response = await _client.GetAsync("/health/ready");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await ApiJson.ReadAsync(response);
        json.GetProperty("checks")[0].GetProperty("name").GetString().ShouldBe("database");
    }

    [Theory]
    [InlineData("/api/v1/projects")]
    [InlineData("/api/v1/users/me")]
    [InlineData("/api/v1/dashboard")]
    public async Task ProtectedEndpoint_Anonymous_Returns401Envelope(string url)
    {
        var response = await _client.GetAsync(url);

        var json = await response.ShouldBeEnvelopeAsync(401);
        json.GetProperty("message").GetString().ShouldBe("Authentication is required.");
        response.Headers.WwwAuthenticate.ShouldContain(h => h.Scheme == "Bearer");
    }

    [Fact]
    public async Task ProtectedEndpoint_GarbageToken_Returns401Envelope()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/projects");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.jwt");

        await (await _client.SendAsync(request)).ShouldBeEnvelopeAsync(401);
    }

    [Fact]
    public async Task InvalidBody_Returns400WithFieldErrors()
    {
        var response = await _client.PostJsonAsync("/api/v1/auth/register", new { firstName = "", email = "not-an-email", password = "short" });

        var json = await response.ShouldBeEnvelopeAsync(400);
        json.GetProperty("message").GetString().ShouldBe("Validation failed.");
        var errors = json.GetProperty("errors");
        errors.TryGetProperty("firstName", out _).ShouldBeTrue();
        errors.TryGetProperty("email", out _).ShouldBeTrue();
        errors.TryGetProperty("password", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task MalformedJson_Returns400Envelope()
    {
        using var content = new StringContent("{\"email\":", Encoding.UTF8, "application/json");

        var json = await (await _client.PostAsync("/api/v1/auth/login", content)).ShouldBeEnvelopeAsync(400);
        json.TryGetProperty("errors", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Responses_CarrySecurityHeaders()
    {
        var response = await _client.GetAsync("/health/live");

        response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        response.Headers.GetValues("X-Frame-Options").ShouldBe(["DENY"]);
        response.Headers.GetValues("Referrer-Policy").ShouldBe(["no-referrer"]);
        response.Headers.GetValues("Content-Security-Policy").Single().ShouldContain("default-src 'none'");
    }

    [Fact]
    public async Task CorsPreflight_AllowedOrigin_AllowsCredentials()
    {
        var response = await Preflight("http://localhost:4200");

        response.Headers.GetValues("Access-Control-Allow-Origin").ShouldBe(["http://localhost:4200"]);
        response.Headers.GetValues("Access-Control-Allow-Credentials").ShouldBe(["true"]);
    }

    [Fact]
    public async Task CorsPreflight_UnknownOrigin_IsNotAllowed()
    {
        var response = await Preflight("https://evil.example");

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    [Fact]
    public async Task Swagger_DocumentIncludesJwtSchemeAndXmlComments()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var document = await response.Content.ReadAsStringAsync();
        document.ShouldContain("\"bearer\"");
        document.ShouldContain("/api/v1/tasks/{id}/status");
        document.ShouldContain("Moves a task to a new status");
        response.Headers.Contains("Content-Security-Policy").ShouldBeFalse();
    }

    private async Task<HttpResponseMessage> Preflight(string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/refresh");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");
        return await _client.SendAsync(request);
    }
}
