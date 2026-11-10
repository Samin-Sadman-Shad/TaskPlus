using FluentValidation;
using SmartTask.Application.DTOs.Tasks;

namespace SmartTask.Application.Validators.Tasks;

/// <summary>
/// Rules for <c>PATCH tasks/{id}/status</c>. This checks only that the value is a known status.
/// Whether the transition is allowed is a domain rule (<c>TaskItem.ChangeStatus</c>), because it
/// depends on the current status.
/// </summary>
public sealed class UpdateTaskStatusDtoValidator : AbstractValidator<UpdateTaskStatusDto>
{
    public UpdateTaskStatusDtoValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum()
            .WithMessage("{PropertyName} must be ToDo, InProgress, Completed or Cancelled.");
    }
}
