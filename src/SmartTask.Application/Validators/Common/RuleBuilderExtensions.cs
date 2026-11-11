using System.Text.RegularExpressions;
using FluentValidation;
using SmartTask.Domain.Constants;

namespace SmartTask.Application.Validators.Common;

/// <summary>
/// Rules shared by several validators, so each rule and its message is written once. Length rules
/// measure the <b>trimmed</b> value, because that is what the domain stores.
/// </summary>
public static partial class RuleBuilderExtensions
{
    /// <summary>Rejects <c>null</c>, empty and whitespace-only strings.</summary>
    public static IRuleBuilderOptions<T, string?> NotBlank<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("{PropertyName} is required.");

    /// <summary>Trimmed length between <paramref name="minLength"/> and <paramref name="maxLength"/>. <c>null</c> passes; combine with <see cref="NotBlank{T}"/>.</summary>
    public static IRuleBuilderOptions<T, string?> TrimmedLength<T>(this IRuleBuilder<T, string?> ruleBuilder, int minLength, int maxLength) =>
        ruleBuilder
            .Must(value => value is null || value.Trim().Length is var length && length >= minLength && length <= maxLength)
            .WithMessage($"{{PropertyName}} must be between {minLength} and {maxLength} characters.");

    /// <summary>Trimmed length at most <paramref name="maxLength"/>. <c>null</c> passes.</summary>
    public static IRuleBuilderOptions<T, string?> TrimmedMaxLength<T>(this IRuleBuilder<T, string?> ruleBuilder, int maxLength) =>
        ruleBuilder
            .Must(value => value is null || value.Trim().Length <= maxLength)
            .WithMessage($"{{PropertyName}} must not exceed {maxLength} characters.");

    /// <summary>Required person name: at most <paramref name="maxLength"/> characters; letters, spaces, hyphens, apostrophes and periods only.</summary>
    public static IRuleBuilderOptions<T, string?> ValidPersonName<T>(this IRuleBuilder<T, string?> ruleBuilder, int maxLength) =>
        ruleBuilder
            .NotBlank()
            .TrimmedMaxLength(maxLength)
            .Must(value => value is null || PersonNamePattern().IsMatch(value.Trim()))
            .WithMessage("{PropertyName} may only contain letters, spaces, hyphens (-), apostrophes (') and periods (.).");

    /// <summary>Required email address of at most <see cref="UserConstraints.EmailMaxLength"/> characters.</summary>
    public static IRuleBuilderOptions<T, string?> ValidEmail<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .NotBlank()
            .TrimmedMaxLength(UserConstraints.EmailMaxLength)
            .EmailAddress()
            .WithMessage("{PropertyName} must be a valid email address.");

    /// <summary>
    /// The password policy for new passwords: <see cref="UserConstraints.PasswordMinLength"/> to
    /// <see cref="UserConstraints.PasswordMaxLength"/> characters, with an upper-case letter, a
    /// lower-case letter, a digit and a symbol. Not used on login.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> ValidPassword<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .NotEmpty()
            .WithMessage("{PropertyName} is required.")
            .Length(UserConstraints.PasswordMinLength, UserConstraints.PasswordMaxLength)
            .WithMessage("{PropertyName} must be between {MinLength} and {MaxLength} characters.")
            .Must(value => value is not null && value.Any(char.IsUpper))
            .WithMessage("{PropertyName} must contain at least one upper-case letter.")
            .Must(value => value is not null && value.Any(char.IsLower))
            .WithMessage("{PropertyName} must contain at least one lower-case letter.")
            .Must(value => value is not null && value.Any(char.IsDigit))
            .WithMessage("{PropertyName} must contain at least one digit.")
            .Must(value => value is not null && value.Any(c => !char.IsLetterOrDigit(c)))
            .WithMessage("{PropertyName} must contain at least one symbol (a character that is not a letter or digit).");

    /// <summary>
    /// The email local part is checked against the password only when it is at least this long.
    /// Otherwise a short address such as <c>a@example.com</c> would forbid every password containing "a".
    /// </summary>
    public const int MinEmailLocalPartLengthToCompare = 3;

    /// <summary>
    /// The password must not contain the local part of the email that <paramref name="email"/> selects
    /// (case-insensitive; only for local parts of at least <see cref="MinEmailLocalPartLengthToCompare"/> characters).
    /// Set the message with <c>WithMessage</c>.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> NotContainEmailLocalPart<T>(this IRuleBuilder<T, string?> ruleBuilder, Func<T, string?> email)
    {
        ArgumentNullException.ThrowIfNull(email);
        return ruleBuilder.Must((instance, password) => !ContainsEmailLocalPart(password, email(instance)));
    }

    /// <summary>
    /// The date must not be earlier than today (UTC) minus <paramref name="toleranceDays"/>. "Today"
    /// is read from <paramref name="timeProvider"/> each time the rule runs, not when the validator is built.
    /// </summary>
    public static IRuleBuilderOptions<T, DateOnly> NotInPast<T>(this IRuleBuilder<T, DateOnly> ruleBuilder, TimeProvider timeProvider, int toleranceDays = 0)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        return ruleBuilder.Must(value => value >= Today(timeProvider).AddDays(-toleranceDays));
    }

    /// <inheritdoc cref="NotInPast{T}(IRuleBuilder{T, DateOnly}, TimeProvider, int)"/>
    /// <remarks><c>null</c> passes.</remarks>
    public static IRuleBuilderOptions<T, DateOnly?> NotInPast<T>(this IRuleBuilder<T, DateOnly?> ruleBuilder, TimeProvider timeProvider, int toleranceDays = 0)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        return ruleBuilder.Must(value => value is null || value >= Today(timeProvider).AddDays(-toleranceDays));
    }

    /// <summary>
    /// Cross-field date rule: the value must be on or after the date <paramref name="other"/> selects.
    /// Passes when either date is missing. Set the message with <c>WithMessage</c>.
    /// </summary>
    public static IRuleBuilderOptions<T, DateOnly?> OnOrAfter<T>(this IRuleBuilder<T, DateOnly?> ruleBuilder, Func<T, DateOnly?> other) =>
        ruleBuilder.Must((instance, value) => value is null || other(instance) is not { } otherValue || value >= otherValue);

    /// <summary>
    /// When <c>SortBy</c> is given it must be one of <paramref name="allowedFields"/> (case-insensitive).
    /// The repositories also whitelist sort keys; this rule gives the client a clear 400 instead of a silent fallback.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> ValidSortField<T>(this IRuleBuilder<T, string?> ruleBuilder, IReadOnlySet<string> allowedFields)
    {
        ArgumentNullException.ThrowIfNull(allowedFields);
        var allowedList = string.Join(", ", allowedFields.Order(StringComparer.OrdinalIgnoreCase));

        return ruleBuilder
            .Must(value => string.IsNullOrWhiteSpace(value) || allowedFields.Contains(value.Trim()))
            .WithMessage($"{{PropertyName}} must be one of: {allowedList}.");
    }

    /// <summary>
    /// Whether <paramref name="password"/> contains the local part of <paramref name="email"/>. Public for
    /// checks that need the stored email, which the request body doesn't carry (password change).
    /// </summary>
    public static bool ContainsEmailLocalPart(string? password, string? email)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var localPart = email.Trim().Split('@')[0];

        return localPart.Length >= MinEmailLocalPartLengthToCompare
            && password.Contains(localPart, StringComparison.OrdinalIgnoreCase);
    }

    private static DateOnly Today(TimeProvider timeProvider) =>
        DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

    /// <summary>Letters (any script, with combining marks), spaces, <c>-</c>, <c>'</c> and <c>.</c>; at least one letter.</summary>
    [GeneratedRegex(@"^(?=.*\p{L})[\p{L}\p{M} .'\-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex PersonNamePattern();
}
