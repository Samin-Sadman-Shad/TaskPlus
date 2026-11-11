using FluentValidation;
using SmartTask.Application.DTOs.Auth;
using SmartTask.Application.Validators.Common;

namespace SmartTask.Application.Validators.Auth;

/// <summary>
/// Shape and password-policy rules for <c>POST auth/register</c>. Whether the email is already taken
/// needs the database, so <c>AuthService</c> checks it, backed by the unique index on <c>NormalizedEmail</c>.
/// </summary>
public sealed class RegisterRequestDtoValidator : PersonNameValidator<RegisterRequestDto>
{
    public RegisterRequestDtoValidator()
    {
        RuleFor(x => x.Email).ValidEmail();

        RuleFor(x => x.Password)
            .ValidPassword()
            .NotContainEmailLocalPart(dto => dto.Email)
            .WithMessage("{PropertyName} must not contain your email address.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password)
            .WithMessage("Passwords do not match.");
    }
}
