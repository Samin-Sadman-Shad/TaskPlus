using FluentValidation.TestHelper;
using SmartTask.Application.DTOs.Auth;
using SmartTask.Application.Validators.Auth;
using SmartTask.Domain.Constants;

namespace SmartTask.Application.UnitTests.Validators.Auth;

public sealed class RegisterRequestDtoValidatorTests
{
    private readonly RegisterRequestDtoValidator _validator = new();

    private static RegisterRequestDto Valid() => new()
    {
        FirstName = "Jane",
        LastName = "O'Neil-Smith",
        Email = TestData.ValidEmail,
        Password = TestData.ValidPassword,
        ConfirmPassword = TestData.ValidPassword,
    };

    [Fact]
    public void Validate_ValidDto_HasNoErrors() =>
        _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankFirstName_HasError(string firstName) =>
        _validator.TestValidate(Valid() with { FirstName = firstName })
            .ShouldHaveValidationErrorFor(x => x.FirstName)
            .WithErrorMessage("First Name is required.");

    [Theory]
    [InlineData(UserConstraints.FirstNameMaxLength, true)]
    [InlineData(UserConstraints.FirstNameMaxLength + 1, false)]
    public void Validate_FirstNameLength_RespectsMaximum(int length, bool valid)
    {
        var result = _validator.TestValidate(Valid() with { FirstName = TestData.Text(length) });

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.FirstName);
        else result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Theory]
    [InlineData(UserConstraints.LastNameMaxLength, true)]
    [InlineData(UserConstraints.LastNameMaxLength + 1, false)]
    public void Validate_LastNameLength_RespectsMaximum(int length, bool valid)
    {
        var result = _validator.TestValidate(Valid() with { LastName = TestData.Text(length) });

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.LastName);
        else result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Theory]
    [InlineData("José", true)]
    [InlineData("Mary-Jane", true)]
    [InlineData("St. John", true)]
    [InlineData("D'Arcy", true)]
    [InlineData("Jane3", false)]
    [InlineData("<script>", false)]
    [InlineData("...", false)]
    public void Validate_FirstNameCharacters_AllowsOnlyNameCharacters(string firstName, bool valid)
    {
        var result = _validator.TestValidate(Valid() with { FirstName = firstName });

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.FirstName);
        else result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Validate_InvalidEmail_HasError(string email) =>
        _validator.TestValidate(Valid() with { Email = email })
            .ShouldHaveValidationErrorFor(x => x.Email);

    [Fact]
    public void Validate_EmailOverMaxLength_HasError()
    {
        var domain = "@example.com";
        var email = TestData.Text(UserConstraints.EmailMaxLength - domain.Length + 1) + domain;

        _validator.TestValidate(Valid() with { Email = email })
            .ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData(UserConstraints.PasswordMinLength - 1, false)]
    [InlineData(UserConstraints.PasswordMinLength, true)]
    [InlineData(UserConstraints.PasswordMaxLength, true)]
    [InlineData(UserConstraints.PasswordMaxLength + 1, false)]
    public void Validate_PasswordLength_RespectsBounds(int length, bool valid)
    {
        var password = "Aa1!" + TestData.Text(length - 4);
        var result = _validator.TestValidate(Valid() with { Password = password, ConfirmPassword = password });

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Password);
        else result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Theory]
    [InlineData("str0ng!pass", "Password must contain at least one upper-case letter.")]
    [InlineData("STR0NG!PASS", "Password must contain at least one lower-case letter.")]
    [InlineData("Strong!Pass", "Password must contain at least one digit.")]
    [InlineData("Str0ngPass1", "Password must contain at least one symbol (a character that is not a letter or digit).")]
    public void Validate_PasswordMissingCharacterClass_HasError(string password, string expectedMessage) =>
        _validator.TestValidate(Valid() with { Password = password, ConfirmPassword = password })
            .ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage(expectedMessage);

    [Fact]
    public void Validate_PasswordContainsEmailLocalPart_HasError()
    {
        const string password = "Jane.Doe!2026";

        _validator.TestValidate(Valid() with { Password = password, ConfirmPassword = password })
            .ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must not contain your email address.");
    }

    [Fact]
    public void Validate_ShortEmailLocalPart_IsNotComparedWithPassword()
    {
        // "ab" is shorter than the minimum compared length, so it must not block a password containing "ab".
        const string password = "Ab0!cdefgh";

        _validator.TestValidate(Valid() with { Email = "ab@example.com", Password = password, ConfirmPassword = password })
            .ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Validate_ConfirmPasswordMismatch_HasError() =>
        _validator.TestValidate(Valid() with { ConfirmPassword = TestData.ValidPassword + "x" })
            .ShouldHaveValidationErrorFor(x => x.ConfirmPassword)
            .WithErrorMessage("Passwords do not match.");
}
