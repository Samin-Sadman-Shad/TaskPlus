using FluentValidation.TestHelper;
using SmartTask.Application.Common.Models;
using SortDirection = SmartTask.Application.Common.Models.SortDirection;
using SmartTask.Application.DTOs.Tasks;
using SmartTask.Application.Validators.Tasks;
using SmartTask.Domain.Constants;
using SmartTask.Domain.Enums;

namespace SmartTask.Application.UnitTests.Validators.Tasks;

public sealed class AssignTaskDtoValidatorTests
{
    private readonly AssignTaskDtoValidator _validator = new();

    [Fact]
    public void Validate_AssigneeId_HasNoErrors() =>
        _validator.TestValidate(new AssignTaskDto { AssigneeId = Guid.NewGuid() }).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_NullAssigneeId_MeansUnassign() =>
        _validator.TestValidate(new AssignTaskDto { AssigneeId = null }).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_EmptyAssigneeId_HasError() =>
        _validator.TestValidate(new AssignTaskDto { AssigneeId = Guid.Empty })
            .ShouldHaveValidationErrorFor(x => x.AssigneeId);
}
