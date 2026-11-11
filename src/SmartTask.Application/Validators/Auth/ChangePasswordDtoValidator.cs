using FluentValidation;
using SmartTask.Application.DTOs.Auth;
using SmartTask.Application.Validators.Common;

namespace SmartTask.Application.Validators.Auth;

/// <summary>
/// Rules for <c>POST users/me/password</c>: the new password follows the normal policy and differs from
/// the current one. Whether the current password is right, and whether the new one contains the user's
/// email, need the stored user, so <c>AuthService</c> checks those.
/// </summary>
public sealed class ChangePasswordDtoValidator : AbstractValidator<ChangePasswordDto>
{
    public ChangePasswordDtoValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty()
            .WithMessage("{PropertyName} is required.");

        RuleFor(x => x.NewPassword)
            .ValidPassword()
            .NotEqual(x => x.CurrentPassword)
            .WithMessage("{PropertyName} must be different from the current password.");

        RuleFor(x => x.ConfirmNewPassword)
            .Equal(x => x.NewPassword)
            .WithMessage("Passwords do not match.");
    }
}
