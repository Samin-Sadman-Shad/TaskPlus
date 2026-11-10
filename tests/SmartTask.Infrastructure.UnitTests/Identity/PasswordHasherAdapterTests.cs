using SmartTask.Application.Contracts.Identity;
using SmartTask.Domain.Entities;
using SmartTask.Infrastructure.Identity;

namespace SmartTask.Infrastructure.UnitTests.Identity;

public sealed class PasswordHasherAdapterTests
{
    private readonly PasswordHasherAdapter _hasher = new();
    private readonly User _user = User.Create("Ada", "Lovelace", "ada@example.com", "placeholder");

    [Fact]
    public void Hash_ThenVerify_Succeeds()
    {
        var hash = _hasher.Hash(_user, "Str0ng!Pass");

        hash.ShouldNotContain("Str0ng!Pass");
        _hasher.Verify(_user, hash, "Str0ng!Pass").ShouldBe(PasswordCheckResult.Success);
    }

    [Fact]
    public void Verify_WrongPassword_Fails()
    {
        var hash = _hasher.Hash(_user, "Str0ng!Pass");

        _hasher.Verify(_user, hash, "wrong").ShouldBe(PasswordCheckResult.Failed);
    }

    [Fact]
    public void Hash_SamePasswordTwice_UsesDifferentSalts() =>
        _hasher.Hash(_user, "Str0ng!Pass").ShouldNotBe(_hasher.Hash(_user, "Str0ng!Pass"));

    [Theory]
    [InlineData("not-base64!!")]
    [InlineData("")]
    public void Verify_MalformedStoredHash_FailsInsteadOfThrowing(string storedHash) =>
        _hasher.Verify(_user, storedHash, "Str0ng!Pass").ShouldBe(PasswordCheckResult.Failed);
}
