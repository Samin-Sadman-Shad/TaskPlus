using FluentValidation.TestHelper;
using SmartTask.Application.DTOs.Auth;
using SmartTask.Application.Validators.Auth;

namespace SmartTask.Application.UnitTests.Validators.Auth;

public sealed class ChangePasswordDtoValidatorTests
{
    private const string NewPassword = "Brand#New2026";

    private readonly ChangePasswordDtoValidator _validator = new();

    private static ChangePasswordDto Valid() => new()
    {
        CurrentPassword = TestData.ValidPassword,
        NewPassword = NewPassword,
        ConfirmNewPassword = NewPassword,
    };

    [Fact]
    public void Validate_ValidDto_HasNoErrors() =>
        _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_MissingCurrentPassword_HasError() =>
        _validator.TestValidate(Valid() with { CurrentPassword = "" })
            .ShouldHaveValidationErrorFor(x => x.CurrentPassword);

    [Theory]
    [InlineData("short1!")]
    [InlineData("alllower1!")]
    [InlineData("NoDigitsHere!")]
    [InlineData("NoSymbol123")]
    public void Validate_NewPasswordBreaksPolicy_HasError(string password) =>
        _validator.TestValidate(Valid() with { NewPassword = password, ConfirmNewPassword = password })
            .ShouldHaveValidationErrorFor(x => x.NewPassword);

    [Fact]
    public void Validate_NewPasswordSameAsCurrent_HasError() =>
        _validator.TestValidate(Valid() with { NewPassword = TestData.ValidPassword, ConfirmNewPassword = TestData.ValidPassword })
            .ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorMessage("New Password must be different from the current password.");

    [Fact]
    public void Validate_ConfirmationMismatch_HasError() =>
        _validator.TestValidate(Valid() with { ConfirmNewPassword = "Other#Pass2026" })
            .ShouldHaveValidationErrorFor(x => x.ConfirmNewPassword)
            .WithErrorMessage("Passwords do not match.");

    [Fact]
    public void ToString_RedactsEveryPassword()
    {
        var text = Valid().ToString();

        text.ShouldNotContain(TestData.ValidPassword);
        text.ShouldNotContain(NewPassword);
    }
}
