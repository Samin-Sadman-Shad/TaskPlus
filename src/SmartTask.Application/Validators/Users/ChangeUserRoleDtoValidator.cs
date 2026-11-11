using FluentValidation;
using SmartTask.Application.DTOs.Users;

namespace SmartTask.Application.Validators.Users;

/// <summary>Rules for <c>PATCH users/{id}/role</c>. The Admin role requirement and the existence of the user are checked elsewhere.</summary>
public sealed class ChangeUserRoleDtoValidator : AbstractValidator<ChangeUserRoleDto>
{
    public ChangeUserRoleDtoValidator()
    {
        RuleFor(x => x.Role)
            .IsInEnum()
            .WithMessage("{PropertyName} must be Admin, ProjectManager or TeamMember.");
    }
}
