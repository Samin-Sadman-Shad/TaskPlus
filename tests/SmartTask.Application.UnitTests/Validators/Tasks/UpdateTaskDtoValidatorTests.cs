using FluentValidation.TestHelper;
using SmartTask.Application.Common.Models;
using SortDirection = SmartTask.Application.Common.Models.SortDirection;
using SmartTask.Application.DTOs.Tasks;
using SmartTask.Application.Validators.Tasks;
using SmartTask.Domain.Constants;
using SmartTask.Domain.Enums;

namespace SmartTask.Application.UnitTests.Validators.Tasks;

public sealed class UpdateTaskDtoValidatorTests
{
    private readonly UpdateTaskDtoValidator _validator = new();

    private static UpdateTaskDto Valid() => new()
    {
        Title = "Write API docs",
        Priority = TaskPriority.Low,
        DueDate = TestData.Today,
    };

    [Fact]
    public void Validate_ValidDto_HasNoErrors() =>
        _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_PastDueDate_IsAllowed() =>
        // Editing an already-overdue task must not fail because of its due date.
        _validator.TestValidate(Valid() with { DueDate = TestData.Today.AddDays(-30) }).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData(TaskItemConstraints.TitleMinLength - 1)]
    [InlineData(TaskItemConstraints.TitleMaxLength + 1)]
    public void Validate_TitleOutOfBounds_SharesCreateRules(int length) =>
        _validator.TestValidate(Valid() with { Title = TestData.Text(length) })
            .ShouldHaveValidationErrorFor(x => x.Title);

    [Fact]
    public void Validate_UndefinedPriority_HasError() =>
        _validator.TestValidate(Valid() with { Priority = (TaskPriority)99 })
            .ShouldHaveValidationErrorFor(x => x.Priority);
}
