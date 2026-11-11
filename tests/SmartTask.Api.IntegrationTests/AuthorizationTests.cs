using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SmartTask.Application.Contracts.Identity;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;
using SmartTask.Infrastructure.Identity;
using SmartTask.Persistence;

namespace SmartTask.Api.IntegrationTests;

/// <summary>
/// Role-based authorization and the 401/403 error codes, through the real pipeline. Users are seeded
/// straight into the database with the role a test needs, and tokens are minted by the app's own
/// <see cref="IJwtTokenService"/>. Hand-signed tokens cover the expired and unknown-role cases.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AuthorizationTests(SmartTaskApiFactory factory)
{
    [Fact]
    public async Task Anonymous_ProtectedEndpoint_Returns401AuthRequired()
    {
        var response = await factory.CreateApiClient().GetAsync("/api/v1/dashboard");

        var json = await response.ShouldBeEnvelopeAsync(401);
        json.ErrorCode().ShouldBe("AUTH_REQUIRED");
        response.Headers.WwwAuthenticate.ShouldContain(h => h.Scheme == "Bearer");
    }

    [Fact]
    public async Task ExpiredToken_Returns401TokenExpired()
    {
        var user = await SeedUserAsync(UserRole.Admin);
        var token = SignToken(user.Id, nameof(UserRole.Admin), expires: DateTime.UtcNow.AddMinutes(-10));

        var json = await (await ClientWith(token).GetAsync("/api/v1/users/me")).ShouldBeEnvelopeAsync(401);

        json.ErrorCode().ShouldBe("TOKEN_EXPIRED");
    }

    [Fact]
    public async Task GarbageToken_Returns401TokenInvalid()
    {
        var json = await (await ClientWith("not.a.jwt").GetAsync("/api/v1/users/me")).ShouldBeEnvelopeAsync(401);

        json.ErrorCode().ShouldBe("TOKEN_INVALID");
    }

    [Theory]
    [InlineData("GET", "/api/v1/users")]
    [InlineData("GET", "/api/v1/users/lookup")]
    [InlineData("POST", "/api/v1/projects")]
    [InlineData("POST", "/api/v1/users")]
    public async Task TeamMember_ManagementEndpoint_Returns403InsufficientRole(string method, string url)
    {
        var client = ClientWith(await TokenForAsync(UserRole.TeamMember));

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url)
        {
            Content = method == "POST" ? JsonContent.Create(new { name = "Nope", startDate = "2026-01-01" }) : null,
        });

        var json = await response.ShouldBeEnvelopeAsync(403);
        json.ErrorCode().ShouldBe("INSUFFICIENT_ROLE");
        json.GetProperty("message").GetString().ShouldBe("You do not have permission to perform this action.");
    }

    [Fact]
    public async Task ProjectManager_CanLookUpUsers_ButNotListThem()
    {
        var client = ClientWith(await TokenForAsync(UserRole.ProjectManager));

        await (await client.GetAsync("/api/v1/users/lookup?search=a")).ShouldBeEnvelopeAsync(200);

        var json = await (await client.GetAsync("/api/v1/users")).ShouldBeEnvelopeAsync(403);
        json.ErrorCode().ShouldBe("INSUFFICIENT_ROLE");
    }

    [Fact]
    public async Task ProjectManager_CannotCreateUsers()
    {
        var client = ClientWith(await TokenForAsync(UserRole.ProjectManager));

        var json = await (await client.PostAsJsonAsync("/api/v1/users", NewUserBody($"pm-made-{Guid.NewGuid():N}@example.com", "Admin")))
            .ShouldBeEnvelopeAsync(403);
        json.ErrorCode().ShouldBe("INSUFFICIENT_ROLE");
    }

    [Fact]
    public async Task Admin_CreatesProjectManager_WhoCanSignInWithThatRole()
    {
        var admin = ClientWith(await TokenForAsync(UserRole.Admin));
        var email = $"new-pm-{Guid.NewGuid():N}@example.com";

        var created = await (await admin.PostAsJsonAsync("/api/v1/users", NewUserBody(email, "ProjectManager"))).ShouldBeEnvelopeAsync(201);
        created.GetProperty("data").GetProperty("role").GetString().ShouldBe("ProjectManager");

        var login = await (await factory.CreateApiClient().PostAsJsonAsync("/api/v1/auth/login", new { email, password = NewUserPassword }))
            .ShouldBeEnvelopeAsync(200);
        login.GetProperty("data").GetProperty("user").GetProperty("role").GetString().ShouldBe("ProjectManager");

        // The same email again is a conflict, not a second account.
        await (await admin.PostAsJsonAsync("/api/v1/users", NewUserBody(email, "TeamMember"))).ShouldBeEnvelopeAsync(409);
    }

    [Fact]
    public async Task Admin_CreateUserWithoutRole_Returns400()
    {
        var admin = ClientWith(await TokenForAsync(UserRole.Admin));

        await (await admin.PostAsJsonAsync("/api/v1/users", new
        {
            firstName = "No",
            lastName = "Role",
            email = $"no-role-{Guid.NewGuid():N}@example.com",
            password = NewUserPassword,
            confirmPassword = NewUserPassword,
        })).ShouldBeEnvelopeAsync(400);
    }

    [Fact]
    public async Task AdminCreatedUser_MustChangePasswordBeforeAnythingElse()
    {
        var admin = ClientWith(await TokenForAsync(UserRole.Admin));
        var email = $"temp-pass-{Guid.NewGuid():N}@example.com";
        await (await admin.PostAsJsonAsync("/api/v1/users", NewUserBody(email, "TeamMember"))).ShouldBeEnvelopeAsync(201);

        // First sign-in with the admin-set password: allowed, but flagged.
        var login = await (await factory.CreateApiClient().PostAsJsonAsync("/api/v1/auth/login", new { email, password = NewUserPassword }))
            .ShouldBeEnvelopeAsync(200);
        login.GetProperty("data").GetProperty("user").GetProperty("mustChangePassword").GetBoolean().ShouldBeTrue();
        var temporary = ClientWith(login.GetProperty("data").GetProperty("accessToken").GetString()!);

        // Everything except the profile and the password change is blocked.
        var blocked = await (await temporary.GetAsync("/api/v1/projects")).ShouldBeEnvelopeAsync(403);
        blocked.ErrorCode().ShouldBe("PASSWORD_CHANGE_REQUIRED");
        await (await temporary.GetAsync("/api/v1/users/me")).ShouldBeEnvelopeAsync(200);

        // The current password is still required.
        await (await temporary.PostAsJsonAsync("/api/v1/users/me/password", new
        {
            currentPassword = "Wrong#Pass2026",
            newPassword = "Chosen#Pass2026",
            confirmNewPassword = "Chosen#Pass2026",
        })).ShouldBeEnvelopeAsync(400);

        var changed = await (await temporary.PostAsJsonAsync("/api/v1/users/me/password", new
        {
            currentPassword = NewUserPassword,
            newPassword = "Chosen#Pass2026",
            confirmNewPassword = "Chosen#Pass2026",
        })).ShouldBeEnvelopeAsync(200);
        changed.GetProperty("data").GetProperty("user").GetProperty("mustChangePassword").GetBoolean().ShouldBeFalse();

        // The new token works everywhere; the old one stays restricted until it expires.
        var unlocked = ClientWith(changed.GetProperty("data").GetProperty("accessToken").GetString()!);
        await (await unlocked.GetAsync("/api/v1/projects")).ShouldBeEnvelopeAsync(200);
        await (await temporary.GetAsync("/api/v1/projects")).ShouldBeEnvelopeAsync(403);

        // The old password no longer signs in; the new one does, without the flag.
        await (await factory.CreateApiClient().PostAsJsonAsync("/api/v1/auth/login", new { email, password = NewUserPassword }))
            .ShouldBeEnvelopeAsync(401);
        var again = await (await factory.CreateApiClient().PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Chosen#Pass2026" }))
            .ShouldBeEnvelopeAsync(200);
        again.GetProperty("data").GetProperty("user").GetProperty("mustChangePassword").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task SelfRegisteredUser_CanChangePassword_AndIsNeverBlocked()
    {
        var client = factory.CreateApiClient();
        var email = $"self-{Guid.NewGuid():N}@example.com";
        await (await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            firstName = "Self", lastName = "Registered", email, password = NewUserPassword, confirmPassword = NewUserPassword,
        })).ShouldBeEnvelopeAsync(201);
        var login = await (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = NewUserPassword })).ShouldBeEnvelopeAsync(200);
        login.GetProperty("data").GetProperty("user").GetProperty("mustChangePassword").GetBoolean().ShouldBeFalse();
        var user = ClientWith(login.GetProperty("data").GetProperty("accessToken").GetString()!);

        await (await user.GetAsync("/api/v1/projects")).ShouldBeEnvelopeAsync(200);
        await (await user.PostAsJsonAsync("/api/v1/users/me/password", new
        {
            currentPassword = NewUserPassword,
            newPassword = "Rotated#Pass2026",
            confirmNewPassword = "Rotated#Pass2026",
        })).ShouldBeEnvelopeAsync(200);
    }

    private const string NewUserPassword = "Create#Pass2026";

    private static object NewUserBody(string email, string role) => new
    {
        firstName = "Made",
        lastName = "ByAdmin",
        email,
        password = NewUserPassword,
        confirmPassword = NewUserPassword,
        role,
    };

    [Fact]
    public async Task Admin_CanListUsers()
    {
        var client = ClientWith(await TokenForAsync(UserRole.Admin));

        await (await client.GetAsync("/api/v1/users")).ShouldBeEnvelopeAsync(200);
    }

    [Theory]
    [InlineData("Hacker")]
    [InlineData("1")]
    [InlineData("admin")]
    [InlineData(null)]
    public async Task ValidSignature_UnknownOrMissingRole_Returns403(string? role)
    {
        var user = await SeedUserAsync(UserRole.Admin);
        var token = SignToken(user.Id, role, expires: DateTime.UtcNow.AddMinutes(5));

        var json = await (await ClientWith(token).GetAsync("/api/v1/users/me")).ShouldBeEnvelopeAsync(403);

        json.ErrorCode().ShouldBe("INSUFFICIENT_ROLE");
    }

    [Fact]
    public async Task TeamMember_ChangingStatusOfSomeoneElsesTask_Returns403ResourceForbidden()
    {
        // The role layer lets a TeamMember call the endpoint; the service refuses because they aren't the assignee.
        var admin = ClientWith(await TokenForAsync(UserRole.Admin));
        var assignee = await SeedUserAsync(UserRole.TeamMember);
        var outsider = await SeedUserAsync(UserRole.TeamMember);

        var project = await (await admin.PostJsonAsync("/api/v1/projects", new
        {
            name = $"Authz {Guid.NewGuid():N}",
            startDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        })).ShouldBeEnvelopeAsync(201);
        var projectId = project.GetProperty("data").GetProperty("id").GetGuid();

        foreach (var member in new[] { assignee, outsider })
        {
            (await admin.PostJsonAsync($"/api/v1/projects/{projectId}/members", new { userId = member.Id }))
                .StatusCode.ShouldBe(System.Net.HttpStatusCode.NoContent);
        }

        var task = await (await admin.PostJsonAsync($"/api/v1/projects/{projectId}/tasks", new
        {
            title = "Someone else's task",
            priority = "Medium",
            assigneeId = assignee.Id,
        })).ShouldBeEnvelopeAsync(201);
        var taskId = task.GetProperty("data").GetProperty("id").GetGuid();

        var outsiderClient = ClientWith(factory.Services.GetRequiredService<IJwtTokenService>().CreateAccessToken(outsider).Token);
        var response = await outsiderClient.PatchAsJsonAsync($"/api/v1/tasks/{taskId}/status", new { status = "InProgress" });

        var json = await response.ShouldBeEnvelopeAsync(403);
        json.ErrorCode().ShouldBe("RESOURCE_FORBIDDEN");
    }

    [Fact]
    public async Task HealthLive_StaysAnonymous()
    {
        var response = await factory.CreateApiClient().GetAsync("/health/live");

        ((int)response.StatusCode).ShouldBe(200);
    }

    private HttpClient ClientWith(string token)
    {
        var client = factory.CreateApiClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<string> TokenForAsync(UserRole role)
    {
        var user = await SeedUserAsync(role);
        return factory.Services.GetRequiredService<IJwtTokenService>().CreateAccessToken(user).Token;
    }

    private async Task<User> SeedUserAsync(UserRole role)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartTaskDbContext>();

        var user = User.Create("Authz", role.ToString(), $"authz-{Guid.NewGuid():N}@example.com", "not-a-real-hash");
        user.ChangeRole(role);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    /// <summary>Signs a token with the app's key, issuer and audience, but with any role (or none) and any expiry.</summary>
    private string SignToken(Guid userId, string? role, DateTime expires)
    {
        var jwt = factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var claims = new Dictionary<string, object> { [AppClaimTypes.Subject] = userId.ToString() };
        if (role is not null)
        {
            claims[AppClaimTypes.Role] = role;
        }

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = expires.AddMinutes(-20),
            NotBefore = expires.AddMinutes(-20),
            Expires = expires,
            Claims = claims,
            SigningCredentials = new SigningCredentials(jwt.CreateSigningKey(), SecurityAlgorithms.HmacSha256),
        });
    }
}

internal static class ErrorCodeJsonExtensions
{
    public static string? ErrorCode(this JsonElement json) =>
        json.TryGetProperty("errorCode", out var code) ? code.GetString() : null;
}
