using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;
using SmartTask.Infrastructure.Identity;

namespace SmartTask.Infrastructure.UnitTests.Identity;

public sealed class JwtTokenServiceTests
{
    private readonly JwtOptions _options = TestJwt.Options();
    private readonly User _user = CreateUser();

    private static User CreateUser()
    {
        var user = User.Create("Ada", "Lovelace", "ada@example.com", "hash");
        user.ChangeRole(UserRole.ProjectManager);
        return user;
    }

    private JwtTokenService Service() => new(TestJwt.Wrap(_options), TestJwt.Clock());

    [Fact]
    public void CreateAccessToken_ContainsExpectedClaims()
    {
        var result = Service().CreateAccessToken(_user);
        var token = new JsonWebToken(result.Token);

        token.Subject.ShouldBe(_user.Id.ToString());
        token.GetClaim(AppClaimTypes.Email).Value.ShouldBe("ada@example.com");
        token.GetClaim(AppClaimTypes.Name).Value.ShouldBe("Ada Lovelace");
        token.GetClaim(AppClaimTypes.Role).Value.ShouldBe("ProjectManager");
        Guid.TryParse(token.Id, out _).ShouldBeTrue();
        token.Issuer.ShouldBe(_options.Issuer);
        token.Audiences.ShouldBe([_options.Audience]);
        token.Alg.ShouldBe(SecurityAlgorithms.HmacSha256);
    }

    [Fact]
    public void CreateAccessToken_NormalUser_HasNoPasswordChangeClaim()
    {
        var token = new JsonWebToken(Service().CreateAccessToken(_user).Token);

        token.TryGetClaim(AppClaimTypes.PasswordChangeRequired, out _).ShouldBeFalse();
    }

    [Fact]
    public void CreateAccessToken_UserWithTemporaryPassword_CarriesPasswordChangeClaim()
    {
        _user.RequirePasswordChange();

        var token = new JsonWebToken(Service().CreateAccessToken(_user).Token);

        token.GetClaim(AppClaimTypes.PasswordChangeRequired).Value.ShouldBe(AppClaimTypes.PasswordChangeRequiredValue);
    }

    [Fact]
    public void CreateAccessToken_UsesConfiguredLifetime()
    {
        var result = Service().CreateAccessToken(_user);
        var token = new JsonWebToken(result.Token);

        result.ExpiresAt.ShouldBe(TestJwt.Now.AddMinutes(15));
        token.ValidTo.ShouldBe(TestJwt.Now.AddMinutes(15).UtcDateTime);
        token.IssuedAt.ShouldBe(TestJwt.Now.UtcDateTime);
    }

    [Fact]
    public void CreateAccessToken_EachTokenHasUniqueId()
    {
        var service = Service();

        var first = new JsonWebToken(service.CreateAccessToken(_user).Token).Id;
        var second = new JsonWebToken(service.CreateAccessToken(_user).Token).Id;

        first.ShouldNotBe(second);
    }

    [Fact]
    public async Task CreateAccessToken_ValidatesWithSameKey_AndFailsWithAnother()
    {
        var token = Service().CreateAccessToken(_user).Token;
        var handler = new JsonWebTokenHandler();

        var valid = await handler.ValidateTokenAsync(token, Parameters(_options.CreateSigningKey()));
        var tampered = await handler.ValidateTokenAsync(token, Parameters(TestJwt.Options(o => o.SigningKey = new string('x', 40)).CreateSigningKey()));

        valid.IsValid.ShouldBeTrue();
        tampered.IsValid.ShouldBeFalse();
    }

    private TokenValidationParameters Parameters(SecurityKey key) => new()
    {
        ValidIssuer = _options.Issuer,
        ValidAudience = _options.Audience,
        IssuerSigningKey = key,
        // The token is issued on the fixed test clock, so don't check its lifetime against the real one.
        ValidateLifetime = false,
    };
}
