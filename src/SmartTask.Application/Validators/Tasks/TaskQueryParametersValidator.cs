using FluentValidation;
using SmartTask.Application.Common.Models;
using SmartTask.Application.Validators.Common;

namespace SmartTask.Application.Validators.Tasks;

/// <summary>Query-string rules for <c>GET tasks</c> and <c>GET projects/{projectId}/tasks</c>.</summary>
public sealed class TaskQueryParametersValidator : PageQueryValidator<TaskQueryParameters>
{
    public TaskQueryParametersValidator()
        : base(TaskQueryParameters.AllowedSortFields)
    {
        RuleFor(x => x.Status)
            .IsInEnum()
            .WithMessage("{PropertyName} must be ToDo, InProgress, Completed or Cancelled.");

        RuleFor(x => x.Priority)
            .IsInEnum()
            .WithMessage("{PropertyName} must be Low, Medium, High or Critical.");

        RuleFor(x => x.DueDateTo)
            .OnOrAfter(x => x.DueDateFrom)
            .WithMessage("{PropertyName} must be on or after Due Date From.");
    }
}
