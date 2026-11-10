using FluentValidation.TestHelper;
using SmartTask.Application.Common.Models;
using SortDirection = SmartTask.Application.Common.Models.SortDirection;
using SmartTask.Application.DTOs.Tasks;
using SmartTask.Application.Validators.Tasks;
using SmartTask.Domain.Constants;
using SmartTask.Domain.Enums;

namespace SmartTask.Application.UnitTests.Validators.Tasks;

public sealed class CreateTaskDtoValidatorTests
{
    private readonly CreateTaskDtoValidator _validator = new(TestData.Clock());

    private static CreateTaskDto Valid() => new()
    {
        Title = "Write API docs",
        Description = "Document every endpoint.",
        Priority = TaskPriority.High,
        DueDate = TestData.Today.AddDays(7),
        AssigneeId = Guid.NewGuid(),
    };

    [Fact]
    public void Validate_ValidDto_HasNoErrors() =>
        _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_MinimalDto_HasNoErrors() =>
        _validator.TestValidate(new CreateTaskDto { Title = "Fix" }).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData(TaskItemConstraints.TitleMinLength - 1, false)]
    [InlineData(TaskItemConstraints.TitleMinLength, true)]
    [InlineData(TaskItemConstraints.TitleMaxLength, true)]
    [InlineData(TaskItemConstraints.TitleMaxLength + 1, false)]
    public void Validate_TitleLength_RespectsBounds(int length, bool valid)
    {
        var result = _validator.TestValidate(Valid() with { Title = TestData.Text(length) });

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Title);
        else result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_WhitespaceTitle_HasRequiredError() =>
        _validator.TestValidate(Valid() with { Title = "   " })
            .ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage("Title is required.");

    [Theory]
    [InlineData(TaskItemConstraints.DescriptionMaxLength, true)]
    [InlineData(TaskItemConstraints.DescriptionMaxLength + 1, false)]
    public void Validate_DescriptionLength_RespectsMaximum(int length, bool valid)
    {
        var result = _validator.TestValidate(Valid() with { Description = TestData.Text(length) });

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Description);
        else result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_UndefinedPriority_HasError() =>
        _validator.TestValidate(Valid() with { Priority = (TaskPriority)0 })
            .ShouldHaveValidationErrorFor(x => x.Priority);

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public void Validate_DueDateRelativeToToday_MustNotBeInPast(int offsetDays, bool valid)
    {
        var result = _validator.TestValidate(Valid() with { DueDate = TestData.Today.AddDays(offsetDays) });

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.DueDate);
        else result.ShouldHaveValidationErrorFor(x => x.DueDate).WithErrorMessage("Due Date cannot be in the past.");
    }

    [Fact]
    public void Validate_EmptyAssigneeId_HasError() =>
        _validator.TestValidate(Valid() with { AssigneeId = Guid.Empty })
            .ShouldHaveValidationErrorFor(x => x.AssigneeId);

    [Fact]
    public void Validate_NullAssigneeId_HasNoErrors() =>
        _validator.TestValidate(Valid() with { AssigneeId = null }).ShouldNotHaveAnyValidationErrors();
}
