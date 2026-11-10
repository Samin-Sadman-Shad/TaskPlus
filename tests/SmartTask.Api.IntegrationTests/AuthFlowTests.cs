using System.Net;
using System.Net.Http.Headers;

namespace SmartTask.Api.IntegrationTests;

/// <summary>Register, login, use the token and log out, through the real pipeline.</summary>
[Collection(ApiCollection.Name)]
public sealed class AuthFlowTests(SmartTaskApiFactory factory)
{
    private const string Password = "Str0ng!Pass";

    [Fact]
    public async Task RegisterLoginAndCallProtectedEndpoint()
    {
        var client = factory.CreateApiClient();
        var email = $"it-{Guid.NewGuid():N}@example.com";

        var registered = await (await client.PostJsonAsync("/api/v1/auth/register", new
        {
            firstName = "Integration",
            lastName = "Tester",
            email,
            password = Password,
            confirmPassword = Password,
            role = "Admin", // must be ignored
        })).ShouldBeEnvelopeAsync(201);
        registered.GetProperty("data").GetProperty("role").GetString().ShouldBe("TeamMember");

        var login = await client.PostJsonAsync("/api/v1/auth/login", new { email, password = Password });
        var loginJson = await login.ShouldBeEnvelopeAsync(200);
        var cookie = login.Headers.GetValues("Set-Cookie").Single();
        cookie.ShouldStartWith("stms_refresh=");
        cookie.ShouldContain("httponly", Case.Insensitive);
        cookie.ShouldContain("secure", Case.Insensitive);
        cookie.ShouldContain("samesite=strict", Case.Insensitive);
        loginJson.GetProperty("data").TryGetProperty("refreshToken", out _).ShouldBeFalse();

        var accessToken = loginJson.GetProperty("data").GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var me = await (await client.GetAsync("/api/v1/users/me")).ShouldBeEnvelopeAsync(200);
        me.GetProperty("data").GetProperty("email").GetString().ShouldBe(email);

        // A team member is authenticated but not an admin: the AdminOnly policy answers 403 in the envelope.
        await (await client.GetAsync("/api/v1/users")).ShouldBeEnvelopeAsync(403);

        var refreshed = await client.PostAsync("/api/v1/auth/refresh", null);
        await refreshed.ShouldBeEnvelopeAsync(200);

        var logout = await client.PostAsync("/api/v1/auth/logout", null);
        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await logout.Content.ReadAsByteArrayAsync()).ShouldBeEmpty();
    }
}
