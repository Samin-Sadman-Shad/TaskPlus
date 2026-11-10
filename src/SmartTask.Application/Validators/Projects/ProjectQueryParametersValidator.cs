using FluentValidation;
using SmartTask.Application.Common.Models;
using SmartTask.Application.Validators.Common;

namespace SmartTask.Application.Validators.Projects;

/// <summary>Query-string rules for <c>GET projects</c>.</summary>
public sealed class ProjectQueryParametersValidator : PageQueryValidator<ProjectQueryParameters>
{
    public ProjectQueryParametersValidator()
        : base(ProjectQueryParameters.AllowedSortFields)
    {
        RuleFor(x => x.StartDateTo)
            .OnOrAfter(x => x.StartDateFrom)
            .WithMessage("{PropertyName} must be on or after Start Date From.");
    }
}
