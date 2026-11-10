using FluentValidation.TestHelper;
using SmartTask.Application.DTOs.Ai;
using SmartTask.Application.Validators.Ai;
using SmartTask.Domain.Constants;

namespace SmartTask.Application.UnitTests.Validators.Ai;

public sealed class ImproveDescriptionRequestDtoValidatorTests
{
    private readonly ImproveDescriptionRequestDtoValidator _validator = new();

    [Fact]
    public void Validate_ValidDto_HasNoErrors() =>
        _validator.TestValidate(new ImproveDescriptionRequestDto { Title = "Login page", Description = "make login work on mobile" })
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_NoTitle_HasNoErrors() =>
        _validator.TestValidate(new ImproveDescriptionRequestDto { Description = "fix the bug" })
            .ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData(AiConstraints.InputMinLength - 1, false)]
    [InlineData(AiConstraints.InputMinLength, true)]
    [InlineData(AiConstraints.InputMaxLength, true)]
    [InlineData(AiConstraints.InputMaxLength + 1, false)]
    public void Validate_DescriptionLength_RespectsBounds(int length, bool valid)
    {
        var result = _validator.TestValidate(new ImproveDescriptionRequestDto { Description = TestData.Text(length) });

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Description);
        else result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_WhitespaceDescription_HasRequiredError() =>
        _validator.TestValidate(new ImproveDescriptionRequestDto { Description = "     " })
            .ShouldHaveValidationErrorFor(x => x.Description)
            .WithErrorMessage("Description is required.");

    [Theory]
    [InlineData(TaskItemConstraints.TitleMaxLength, true)]
    [InlineData(TaskItemConstraints.TitleMaxLength + 1, false)]
    public void Validate_TitleLength_RespectsMaximum(int length, bool valid)
    {
        var result = _validator.TestValidate(new ImproveDescriptionRequestDto { Title = TestData.Text(length), Description = "fix the bug" });

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Title);
        else result.ShouldHaveValidationErrorFor(x => x.Title);
    }
}
