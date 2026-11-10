using FluentValidation;
using SmartTask.Application.DTOs.Projects;
using SmartTask.Application.Validators.Common;

namespace SmartTask.Application.Validators.Projects;

/// <summary>Rules for <c>POST projects</c>: the shared project rules plus a start date that is not in the past.</summary>
public sealed class CreateProjectDtoValidator : ProjectDetailsValidator<CreateProjectDto>
{
    /// <summary>
    /// A new project may start up to this many days before today (UTC). This allows for a client whose
    /// local date is behind UTC.
    /// </summary>
    internal const int StartDateToleranceDays = 1;

    public CreateProjectDtoValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.StartDate)
            .NotInPast(timeProvider, StartDateToleranceDays)
            .When(x => x.StartDate != default)
            .WithMessage($"{{PropertyName}} cannot be more than {StartDateToleranceDays} day in the past.");
    }
}
