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
    /// <summary>
    /// The email local part is checked against the password only when it is at least this long.
    /// Otherwise a short address such as <c>a@example.com</c> would forbid every password containing "a".
    /// </summary>
    internal const int MinLocalPartLengthToCompare = 3;

    public RegisterRequestDtoValidator()
    {
        RuleFor(x => x.Email).ValidEmail();

        RuleFor(x => x.Password)
            .ValidPassword()
            .Must((dto, password) => !ContainsEmailLocalPart(password, dto.Email))
            .WithMessage("{PropertyName} must not contain your email address.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password)
            .WithMessage("Passwords do not match.");
    }

    private static bool ContainsEmailLocalPart(string? password, string? email)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var localPart = email.Trim().Split('@')[0];

        return localPart.Length >= MinLocalPartLengthToCompare
            && password.Contains(localPart, StringComparison.OrdinalIgnoreCase);
    }
}
