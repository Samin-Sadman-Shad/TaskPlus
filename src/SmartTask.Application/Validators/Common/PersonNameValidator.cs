using FluentValidation;
using SmartTask.Application.DTOs.Users;
using SmartTask.Domain.Constants;

namespace SmartTask.Application.Validators.Common;

/// <summary>First and last name rules, shared by registration and profile update.</summary>
public abstract class PersonNameValidator<T> : AbstractValidator<T>
    where T : IPersonName
{
    protected PersonNameValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.FirstName).ValidPersonName(UserConstraints.FirstNameMaxLength);
        RuleFor(x => x.LastName).ValidPersonName(UserConstraints.LastNameMaxLength);
    }
}
