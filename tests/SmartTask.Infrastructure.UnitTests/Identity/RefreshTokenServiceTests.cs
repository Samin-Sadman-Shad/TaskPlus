using System.Buffers.Text;
using SmartTask.Domain.Constants;
using SmartTask.Infrastructure.Identity;

namespace SmartTask.Infrastructure.UnitTests.Identity;

public sealed class RefreshTokenServiceTests
{
    private readonly RefreshTokenService _service = new(TestJwt.Wrap(TestJwt.Options()), TestJwt.Clock());

    [Fact]
    public void Generate_Returns64RandomBytesAsBase64Url()
    {
        var token = _service.Generate();

        Base64Url.DecodeFromChars(token.RawToken).Length.ShouldBe(RefreshTokenService.TokenByteLength);
        token.RawToken.ShouldNotContain('+');
        token.RawToken.ShouldNotContain('/');
        token.RawToken.ShouldNotContain('=');
    }

    [Fact]
    public void Generate_HashMatchesHashOfRawToken_AndFitsColumn()
    {
        var token = _service.Generate();

        token.TokenHash.ShouldBe(_service.Hash(token.RawToken));
        token.TokenHash.ShouldNotBe(token.RawToken);
        token.TokenHash.Length.ShouldBeLessThanOrEqualTo(RefreshTokenConstraints.TokenHashMaxLength);
    }

    [Fact]
    public void Generate_ExpiresAfterConfiguredDays() =>
        _service.Generate().ExpiresAt.ShouldBe(TestJwt.Now.AddDays(7));

    [Fact]
    public void Generate_ProducesDifferentTokensEachTime() =>
        _service.Generate().RawToken.ShouldNotBe(_service.Generate().RawToken);

    [Fact]
    public void Hash_IsDeterministicLowerCaseSha256Hex()
    {
        // SHA-256("abc"), a standard test vector.
        _service.Hash("abc").ShouldBe("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }
}
