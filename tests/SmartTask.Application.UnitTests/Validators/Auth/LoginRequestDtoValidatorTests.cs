using FluentValidation.TestHelper;
using SmartTask.Application.DTOs.Auth;
using SmartTask.Application.Validators.Auth;
using SmartTask.Domain.Constants;

namespace SmartTask.Application.UnitTests.Validators.Auth;

public sealed class LoginRequestDtoValidatorTests
{
    private readonly LoginRequestDtoValidator _validator = new();

    [Fact]
    public void Validate_ValidDto_HasNoErrors() =>
        _validator.TestValidate(new LoginRequestDto { Email = TestData.ValidEmail, Password = TestData.ValidPassword })
            .ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("password")]
    [InlineData("x")]
    public void Validate_WeakPassword_IsAccepted(string password) =>
        // Login must not apply (or reveal) the complexity policy.
        _validator.TestValidate(new LoginRequestDto { Email = TestData.ValidEmail, Password = password })
            .ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("not-an-email")]
    public void Validate_InvalidEmail_HasError(string email) =>
        _validator.TestValidate(new LoginRequestDto { Email = email, Password = TestData.ValidPassword })
            .ShouldHaveValidationErrorFor(x => x.Email);

    [Fact]
    public void Validate_EmptyPassword_HasError() =>
        _validator.TestValidate(new LoginRequestDto { Email = TestData.ValidEmail, Password = "" })
            .ShouldHaveValidationErrorFor(x => x.Password);

    [Theory]
    [InlineData(UserConstraints.PasswordMaxLength, true)]
    [InlineData(UserConstraints.PasswordMaxLength + 1, false)]
    public void Validate_PasswordLength_RespectsMaximum(int length, bool valid)
    {
        var result = _validator.TestValidate(new LoginRequestDto { Email = TestData.ValidEmail, Password = TestData.Text(length) });

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Password);
        else result.ShouldHaveValidationErrorFor(x => x.Password);
    }
}
