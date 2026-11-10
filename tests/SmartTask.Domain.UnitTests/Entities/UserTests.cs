using Shouldly;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;
using SmartTask.Domain.Exceptions;

namespace SmartTask.Domain.UnitTests.Entities;

public sealed class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_ValidInput_NormalisesEmail()
    {
        var user = User.Create("Ada", "Lovelace", "  Ada.Lovelace@Example.com  ", "hash");

        user.Email.ShouldBe("Ada.Lovelace@Example.com");
        user.NormalizedEmail.ShouldBe("ADA.LOVELACE@EXAMPLE.COM");
    }

    [Fact]
    public void Create_ValidInput_IsActiveTeamMember()
    {
        var user = User.Create(" Ada ", " Lovelace ", "ada@example.com", "hash");

        user.Role.ShouldBe(UserRole.TeamMember);
        user.IsActive.ShouldBeTrue();
        user.FullName.ShouldBe("Ada Lovelace");
    }

    [Theory]
    [InlineData("", "Lovelace", "ada@example.com", "hash")]
    [InlineData("Ada", " ", "ada@example.com", "hash")]
    [InlineData("Ada", "Lovelace", "", "hash")]
    [InlineData("Ada", "Lovelace", "ada@example.com", "")]
    public void Create_MissingRequiredValue_ThrowsDomainException(string first, string last, string email, string hash)
    {
        Should.Throw<DomainException>(() => User.Create(first, last, email, hash));
    }

    [Fact]
    public void ChangePasswordHash_WithActiveTokens_RevokesOnlyActiveTokens()
    {
        var user = User.Create("Ada", "Lovelace", "ada@example.com", "old-hash");
        var active = RefreshToken.Create(user.Id, "active", Now.AddDays(7), Now.AddDays(-1), null);
        var expired = RefreshToken.Create(user.Id, "expired", Now.AddMinutes(-1), Now.AddDays(-8), null);
        user.AddRefreshToken(active);
        user.AddRefreshToken(expired);

        user.ChangePasswordHash("new-hash", Now);

        user.PasswordHash.ShouldBe("new-hash");
        active.IsActive(Now).ShouldBeFalse();
        active.RevokedAt.ShouldBe(Now);
        expired.RevokedAt.ShouldBeNull();
    }

    [Fact]
    public void RehashPassword_ReplacesHashAndKeepsSessions()
    {
        var user = User.Create("Ada", "Lovelace", "ada@example.com", "old-hash");
        var active = RefreshToken.Create(user.Id, "active", Now.AddDays(7), Now.AddDays(-1), null);
        user.AddRefreshToken(active);

        user.RehashPassword("stronger-hash");

        user.PasswordHash.ShouldBe("stronger-hash");
        active.IsActive(Now).ShouldBeTrue();
    }

    [Fact]
    public void RehashPassword_Blank_ThrowsDomainException()
    {
        var user = User.Create("Ada", "Lovelace", "ada@example.com", "hash");

        Should.Throw<DomainException>(() => user.RehashPassword(" "));
    }

    [Fact]
    public void AddRefreshToken_TokenForOtherUser_ThrowsDomainException()
    {
        var user = User.Create("Ada", "Lovelace", "ada@example.com", "hash");
        var token = RefreshToken.Create(Guid.NewGuid(), "hash", Now.AddDays(7), Now, null);

        Should.Throw<DomainException>(() => user.AddRefreshToken(token));
    }

    [Fact]
    public void ChangeRole_UndefinedRole_ThrowsDomainException()
    {
        var user = User.Create("Ada", "Lovelace", "ada@example.com", "hash");

        Should.Throw<DomainException>(() => user.ChangeRole((UserRole)0));
    }
}
