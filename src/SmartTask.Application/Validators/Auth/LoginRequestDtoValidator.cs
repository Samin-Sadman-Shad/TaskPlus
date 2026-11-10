using FluentValidation;
using SmartTask.Application.DTOs.Auth;
using SmartTask.Application.Validators.Common;
using SmartTask.Domain.Constants;

namespace SmartTask.Application.Validators.Auth;

/// <summary>
/// Rules for <c>POST auth/login</c>. The password complexity policy is deliberately <b>not</b>
/// applied. It would reveal the policy to an attacker and lock out passwords created under an older
/// policy. The maximum length bounds hashing cost. Whether the credentials are correct is checked by <c>AuthService</c>.
/// </summary>
public sealed class LoginRequestDtoValidator : AbstractValidator<LoginRequestDto>
{
    public LoginRequestDtoValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Email)
            .NotBlank()
            .EmailAddress()
            .WithMessage("{PropertyName} must be a valid email address.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("{PropertyName} is required.")
            .MaximumLength(UserConstraints.PasswordMaxLength)
            .WithMessage("{PropertyName} must not exceed {MaxLength} characters.");
    }
}
