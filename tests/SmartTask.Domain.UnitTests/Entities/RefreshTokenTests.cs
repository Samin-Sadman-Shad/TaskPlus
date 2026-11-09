using Shouldly;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Exceptions;

namespace SmartTask.Domain.UnitTests.Entities;

public sealed class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IsActive_NotRevokedAndNotExpired_ReturnsTrue()
    {
        var token = CreateToken(expiresAt: Now.AddDays(7));

        token.IsActive(Now).ShouldBeTrue();
    }

    [Fact]
    public void IsActive_Expired_ReturnsFalse()
    {
        var token = CreateToken(expiresAt: Now.AddDays(7));

        token.IsExpired(Now.AddDays(7)).ShouldBeTrue();
        token.IsActive(Now.AddDays(7)).ShouldBeFalse();
    }

    [Fact]
    public void IsActive_Revoked_ReturnsFalse()
    {
        var token = CreateToken(expiresAt: Now.AddDays(7));

        token.Revoke(Now, "Rotated", "new-hash");

        token.IsActive(Now).ShouldBeFalse();
        token.RevokedReason.ShouldBe("Rotated");
        token.ReplacedByTokenHash.ShouldBe("new-hash");
    }

    [Fact]
    public void Revoke_AlreadyRevoked_ThrowsDomainException()
    {
        var token = CreateToken(expiresAt: Now.AddDays(7));
        token.Revoke(Now, "Logout");

        Should.Throw<DomainException>(() => token.Revoke(Now, "Logout"));
    }

    [Fact]
    public void Create_ExpiryNotAfterCreation_ThrowsDomainException()
    {
        Should.Throw<DomainException>(() => CreateToken(expiresAt: Now));
    }

    private static RefreshToken CreateToken(DateTimeOffset expiresAt) =>
        RefreshToken.Create(Guid.NewGuid(), "token-hash", expiresAt, Now, "127.0.0.1");
}
