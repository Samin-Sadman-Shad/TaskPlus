using FluentValidation;
using SmartTask.Application.DTOs.Projects;

namespace SmartTask.Application.Validators.Projects;

/// <summary>
/// Rules for <c>POST projects/{id}/members</c>. Whether the user exists and is active needs the
/// database, so <c>ProjectService</c> checks it.
/// </summary>
public sealed class AddProjectMemberDtoValidator : AbstractValidator<AddProjectMemberDto>
{
    public AddProjectMemberDtoValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("{PropertyName} is required.");
    }
}
