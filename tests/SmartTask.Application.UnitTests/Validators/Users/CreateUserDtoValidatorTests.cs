using FluentValidation.TestHelper;
using SmartTask.Application.DTOs.Users;
using SmartTask.Application.Validators.Users;
using SmartTask.Domain.Constants;
using SmartTask.Domain.Enums;

namespace SmartTask.Application.UnitTests.Validators.Users;

public sealed class CreateUserDtoValidatorTests
{
    private readonly CreateUserDtoValidator _validator = new();

    private static CreateUserDto Valid() => new()
    {
        FirstName = "Pat",
        LastName = "O'Manager",
        Email = TestData.ValidEmail,
        Password = TestData.ValidPassword,
        ConfirmPassword = TestData.ValidPassword,
        Role = UserRole.ProjectManager,
    };

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.ProjectManager)]
    [InlineData(UserRole.TeamMember)]
    public void Validate_ValidDtoForEveryRole_HasNoErrors(UserRole role) =>
        _validator.TestValidate(Valid() with { Role = role }).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData(0)]   // role omitted from the JSON body
    [InlineData(99)]
    public void Validate_MissingOrUnknownRole_HasError(int role) =>
        _validator.TestValidate(Valid() with { Role = (UserRole)role })
            .ShouldHaveValidationErrorFor(x => x.Role)
            .WithErrorMessage("Role must be Admin, ProjectManager or TeamMember.");

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Validate_InvalidEmail_HasError(string email) =>
        _validator.TestValidate(Valid() with { Email = email }).ShouldHaveValidationErrorFor(x => x.Email);

    [Theory]
    [InlineData("short1!")]       // too short
    [InlineData("alllower1!")]    // no upper case
    [InlineData("NoDigitsHere!")] // no digit
    [InlineData("NoSymbol123")]   // no symbol
    public void Validate_PasswordBreaksPolicy_HasError(string password) =>
        _validator.TestValidate(Valid() with { Password = password, ConfirmPassword = password })
            .ShouldHaveValidationErrorFor(x => x.Password);

    [Fact]
    public void Validate_PasswordContainsEmailLocalPart_HasError() =>
        _validator.TestValidate(Valid() with { Password = "Jane.Doe#2026", ConfirmPassword = "Jane.Doe#2026" })
            .ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must not contain the email address.");

    [Fact]
    public void Validate_ConfirmPasswordMismatch_HasError() =>
        _validator.TestValidate(Valid() with { ConfirmPassword = "Different1!" })
            .ShouldHaveValidationErrorFor(x => x.ConfirmPassword)
            .WithErrorMessage("Passwords do not match.");

    [Fact]
    public void Validate_FirstNameTooLong_HasError() =>
        _validator.TestValidate(Valid() with { FirstName = TestData.Text(UserConstraints.FirstNameMaxLength + 1) })
            .ShouldHaveValidationErrorFor(x => x.FirstName);

    [Fact]
    public void ToString_RedactsPasswords() =>
        Valid().ToString().ShouldNotContain(TestData.ValidPassword);
}
