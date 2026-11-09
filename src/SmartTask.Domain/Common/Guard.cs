using SmartTask.Domain.Exceptions;

namespace SmartTask.Domain.Common;

/// <summary>
/// Guard clauses shared by the entities, so every invariant fails with the same
/// <see cref="DomainException"/> shape instead of repeating the checks in each entity.
/// </summary>
internal static class Guard
{
    /// <summary>Trims <paramref name="value"/> and checks it is non-empty and within the length bounds.</summary>
    public static string RequiredText(string? value, string name, int maxLength, int minLength = 1)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainException($"{name} is required.");
        }

        if (trimmed.Length < minLength)
        {
            throw new DomainException($"{name} must be at least {minLength} characters.");
        }

        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{name} must not exceed {maxLength} characters.");
        }

        return trimmed;
    }

    /// <summary>Trims <paramref name="value"/>; blank becomes <c>null</c>. Checks the maximum length.</summary>
    public static string? OptionalText(string? value, string name, int maxLength)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{name} must not exceed {maxLength} characters.");
        }

        return trimmed;
    }

    public static Guid NotEmpty(Guid value, string name)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException($"{name} is required.");
        }

        return value;
    }

    public static TEnum DefinedEnum<TEnum>(TEnum value, string name)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new DomainException($"{name} has an invalid value '{value}'.");
        }

        return value;
    }
}
