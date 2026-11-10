using FluentValidation;
using SmartTask.Application.Common.Models;
using SmartTask.Application.Validators.Common;

namespace SmartTask.Application.Validators.Users;

/// <summary>Query-string rules for <c>GET users</c>.</summary>
public sealed class UserQueryParametersValidator : PageQueryValidator<UserQueryParameters>
{
    public UserQueryParametersValidator()
        : base(UserQueryParameters.AllowedSortFields)
    {
        RuleFor(x => x.Role)
            .IsInEnum()
            .WithMessage("{PropertyName} must be Admin, ProjectManager or TeamMember.");
    }
}
