using FluentValidation;
using SmartTask.Application.DTOs.Users;
using SmartTask.Application.Validators.Common;

namespace SmartTask.Application.Validators.Users;

/// <summary>
/// Rules for <c>POST users</c> (admin only), also used to check the bootstrap admin settings at start-up.
/// The same name, email and password policy as registration, plus a required role. Whether the email is
/// taken needs the database, so <c>UserService</c> checks it, backed by the unique index.
/// </summary>
public sealed class CreateUserDtoValidator : PersonNameValidator<CreateUserDto>
{
    public CreateUserDtoValidator()
    {
        RuleFor(x => x.Email).ValidEmail();

        RuleFor(x => x.Password)
            .ValidPassword()
            .NotContainEmailLocalPart(dto => dto.Email)
            .WithMessage("{PropertyName} must not contain the email address.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password)
            .WithMessage("Passwords do not match.");

        RuleFor(x => x.Role)
            .IsInEnum()
            .WithMessage("{PropertyName} must be Admin, ProjectManager or TeamMember.");
    }
}
