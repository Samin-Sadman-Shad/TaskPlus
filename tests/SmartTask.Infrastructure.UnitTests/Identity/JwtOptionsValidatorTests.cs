using SmartTask.Infrastructure.Identity;

namespace SmartTask.Infrastructure.UnitTests.Identity;

public sealed class JwtOptionsValidatorTests
{
    private readonly JwtOptionsValidator _validator = new();

    [Fact]
    public void Validate_CompleteOptions_Succeeds() =>
        _validator.Validate(null, TestJwt.Options()).Succeeded.ShouldBeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("too-short-key")]
    public void Validate_MissingOrWeakSigningKey_Fails(string key)
    {
        var result = _validator.Validate(null, TestJwt.Options(o => o.SigningKey = key));

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldNotBeNull().ShouldContain("Jwt:SigningKey");
    }

    [Fact]
    public void Validate_WeakSigningKey_NeverEchoesTheKey() =>
        _validator.Validate(null, TestJwt.Options(o => o.SigningKey = "secret-value-123"))
            .FailureMessage.ShouldNotBeNull().ShouldNotContain("secret-value-123");

    [Fact]
    public void Validate_KeyOfExactlyMinimumBytes_Succeeds() =>
        _validator.Validate(null, TestJwt.Options(o => o.SigningKey = new string('k', JwtOptions.MinSigningKeyBytes)))
            .Succeeded.ShouldBeTrue();

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public void Validate_AccessTokenMinutesOutOfRange_Fails(int minutes) =>
        _validator.Validate(null, TestJwt.Options(o => o.AccessTokenMinutes = minutes)).Failed.ShouldBeTrue();

    [Fact]
    public void Validate_MissingIssuerAndAudience_ReportsBoth()
    {
        var result = _validator.Validate(null, TestJwt.Options(o => { o.Issuer = ""; o.Audience = " "; }));

        var message = result.FailureMessage.ShouldNotBeNull();
        message.ShouldContain("Jwt:Issuer");
        message.ShouldContain("Jwt:Audience");
    }
}
