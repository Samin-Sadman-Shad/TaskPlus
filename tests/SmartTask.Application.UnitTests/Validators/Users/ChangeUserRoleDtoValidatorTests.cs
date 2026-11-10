using FluentValidation.TestHelper;
using SmartTask.Application.Common.Models;
using SmartTask.Application.DTOs.Users;
using SmartTask.Application.Validators.Users;
using SmartTask.Domain.Constants;
using SmartTask.Domain.Enums;

namespace SmartTask.Application.UnitTests.Validators.Users;

public sealed class ChangeUserRoleDtoValidatorTests
{
    private readonly ChangeUserRoleDtoValidator _validator = new();

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.ProjectManager)]
    [InlineData(UserRole.TeamMember)]
    public void Validate_DefinedRole_HasNoErrors(UserRole role) =>
        _validator.TestValidate(new ChangeUserRoleDto { Role = role }).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void Validate_UndefinedRole_HasError(int role) =>
        _validator.TestValidate(new ChangeUserRoleDto { Role = (UserRole)role })
            .ShouldHaveValidationErrorFor(x => x.Role);
}
