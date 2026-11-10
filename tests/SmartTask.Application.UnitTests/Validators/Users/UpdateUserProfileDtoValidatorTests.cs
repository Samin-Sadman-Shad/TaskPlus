using FluentValidation.TestHelper;
using SmartTask.Application.Common.Models;
using SmartTask.Application.DTOs.Users;
using SmartTask.Application.Validators.Users;
using SmartTask.Domain.Constants;
using SmartTask.Domain.Enums;

namespace SmartTask.Application.UnitTests.Validators.Users;

public sealed class UpdateUserProfileDtoValidatorTests
{
    private readonly UpdateUserProfileDtoValidator _validator = new();

    [Fact]
    public void Validate_ValidDto_HasNoErrors() =>
        _validator.TestValidate(new UpdateUserProfileDto { FirstName = "Jane", LastName = "Doe" })
            .ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("J4ne")]
    public void Validate_InvalidFirstName_HasError(string firstName) =>
        _validator.TestValidate(new UpdateUserProfileDto { FirstName = firstName, LastName = "Doe" })
            .ShouldHaveValidationErrorFor(x => x.FirstName);

    [Theory]
    [InlineData(UserConstraints.LastNameMaxLength, true)]
    [InlineData(UserConstraints.LastNameMaxLength + 1, false)]
    public void Validate_LastNameLength_RespectsMaximum(int length, bool valid)
    {
        var result = _validator.TestValidate(new UpdateUserProfileDto { FirstName = "Jane", LastName = TestData.Text(length) });

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.LastName);
        else result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Fact]
    public void Validate_NameWithSurroundingSpaces_MeasuresTrimmedLength() =>
        _validator.TestValidate(new UpdateUserProfileDto { FirstName = $"  {TestData.Text(UserConstraints.FirstNameMaxLength)}  ", LastName = "Doe" })
            .ShouldNotHaveValidationErrorFor(x => x.FirstName);
}
