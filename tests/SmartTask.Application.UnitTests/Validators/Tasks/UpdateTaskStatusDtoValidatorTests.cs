using FluentValidation.TestHelper;
using SmartTask.Application.Common.Models;
using SortDirection = SmartTask.Application.Common.Models.SortDirection;
using SmartTask.Application.DTOs.Tasks;
using SmartTask.Application.Validators.Tasks;
using SmartTask.Domain.Constants;
using SmartTask.Domain.Enums;

namespace SmartTask.Application.UnitTests.Validators.Tasks;

public sealed class UpdateTaskStatusDtoValidatorTests
{
    private readonly UpdateTaskStatusDtoValidator _validator = new();

    [Theory]
    [InlineData(TaskItemStatus.ToDo)]
    [InlineData(TaskItemStatus.InProgress)]
    [InlineData(TaskItemStatus.Completed)]
    [InlineData(TaskItemStatus.Cancelled)]
    public void Validate_DefinedStatus_HasNoErrors(TaskItemStatus status) =>
        _validator.TestValidate(new UpdateTaskStatusDto { Status = status }).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void Validate_UndefinedStatus_HasError(int status) =>
        _validator.TestValidate(new UpdateTaskStatusDto { Status = (TaskItemStatus)status })
            .ShouldHaveValidationErrorFor(x => x.Status);
}
