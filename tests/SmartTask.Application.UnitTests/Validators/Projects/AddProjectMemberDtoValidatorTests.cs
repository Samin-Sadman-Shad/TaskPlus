using FluentValidation.TestHelper;
using SmartTask.Application.Common.Models;
using SmartTask.Application.DTOs.Projects;
using SmartTask.Application.Validators.Projects;
using SmartTask.Domain.Constants;

namespace SmartTask.Application.UnitTests.Validators.Projects;

public sealed class AddProjectMemberDtoValidatorTests
{
    private readonly AddProjectMemberDtoValidator _validator = new();

    [Fact]
    public void Validate_UserId_HasNoErrors() =>
        _validator.TestValidate(new AddProjectMemberDto { UserId = Guid.NewGuid() }).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_EmptyUserId_HasError() =>
        _validator.TestValidate(new AddProjectMemberDto { UserId = Guid.Empty })
            .ShouldHaveValidationErrorFor(x => x.UserId);
}
