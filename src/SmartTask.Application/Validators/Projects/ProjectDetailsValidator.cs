using FluentValidation;
using SmartTask.Application.DTOs.Projects;
using SmartTask.Application.Validators.Common;
using SmartTask.Domain.Constants;

namespace SmartTask.Application.Validators.Projects;

/// <summary>
/// Rules shared by create and update. Name uniqueness per owner needs the database, so
/// <c>ProjectService</c> checks it, backed by the filtered unique index on <c>(OwnerId, Name)</c>.
/// </summary>
public abstract class ProjectDetailsValidator<T> : AbstractValidator<T>
    where T : IProjectDetails
{
    protected ProjectDetailsValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Name)
            .NotBlank()
            .TrimmedLength(ProjectConstraints.NameMinLength, ProjectConstraints.NameMaxLength);

        RuleFor(x => x.Description)
            .TrimmedMaxLength(ProjectConstraints.DescriptionMaxLength);

        RuleFor(x => x.StartDate)
            .NotEmpty()
            .WithMessage("{PropertyName} is required.");

        RuleFor(x => x.EndDate)
            .OnOrAfter(x => x.StartDate)
            .WithMessage("End date must be on or after the start date.");
    }
}
