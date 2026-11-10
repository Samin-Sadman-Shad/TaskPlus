using FluentValidation;
using SmartTask.Application.DTOs.Tasks;
using SmartTask.Application.Validators.Common;
using SmartTask.Domain.Constants;

namespace SmartTask.Application.Validators.Tasks;

/// <summary>Rules shared by task create and update.</summary>
public abstract class TaskDetailsValidator<T> : AbstractValidator<T>
    where T : ITaskDetails
{
    protected TaskDetailsValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Title)
            .NotBlank()
            .TrimmedLength(TaskItemConstraints.TitleMinLength, TaskItemConstraints.TitleMaxLength);

        RuleFor(x => x.Description)
            .TrimmedMaxLength(TaskItemConstraints.DescriptionMaxLength);

        RuleFor(x => x.Priority)
            .IsInEnum()
            .WithMessage("{PropertyName} must be Low, Medium, High or Critical.");
    }
}
