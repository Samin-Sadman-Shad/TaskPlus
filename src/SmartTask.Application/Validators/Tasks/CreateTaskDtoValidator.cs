using FluentValidation;
using SmartTask.Application.DTOs.Tasks;
using SmartTask.Application.Validators.Common;

namespace SmartTask.Application.Validators.Tasks;

/// <summary>
/// Rules for <c>POST projects/{projectId}/tasks</c>. Whether the assignee exists and is a project
/// member needs the database, so <c>TaskService</c> checks it.
/// </summary>
public sealed class CreateTaskDtoValidator : TaskDetailsValidator<CreateTaskDto>
{
    public CreateTaskDtoValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.DueDate)
            .NotInPast(timeProvider)
            .WithMessage("{PropertyName} cannot be in the past.");

        RuleFor(x => x.AssigneeId)
            .NotEqual(Guid.Empty)
            .When(x => x.AssigneeId.HasValue)
            .WithMessage("{PropertyName} must be a valid user id.");
    }
}
