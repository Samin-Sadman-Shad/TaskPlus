using FluentValidation;
using SmartTask.Application.DTOs.Tasks;

namespace SmartTask.Application.Validators.Tasks;

/// <summary>
/// Rules for <c>PATCH tasks/{id}/assignee</c>. <c>null</c> (unassign) is valid. Project membership
/// needs the database, so <c>TaskService</c> checks it.
/// </summary>
public sealed class AssignTaskDtoValidator : AbstractValidator<AssignTaskDto>
{
    public AssignTaskDtoValidator()
    {
        RuleFor(x => x.AssigneeId)
            .NotEqual(Guid.Empty)
            .When(x => x.AssigneeId.HasValue)
            .WithMessage("{PropertyName} must be a valid user id, or null to unassign.");
    }
}
