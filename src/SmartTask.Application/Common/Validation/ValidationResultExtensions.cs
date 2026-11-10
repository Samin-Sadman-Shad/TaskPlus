using System.Text.Json;
using FluentValidation.Results;

namespace SmartTask.Application.Common.Validation;

public static class ValidationResultExtensions
{
    /// <summary>
    /// Groups the failures by property, with camelCase keys that match the JSON the client sent
    /// (<c>EndDate</c> → <c>endDate</c>, <c>Members[0].UserId</c> → <c>members[0].userId</c>).
    /// Used by the API's <c>ValidationFilter</c> to build <c>BaseResponse.ValidationFailed(errors)</c>.
    /// </summary>
    public static IReadOnlyDictionary<string, string[]> ToErrorDictionary(this ValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.Errors
            .GroupBy(e => ToCamelCasePath(e.PropertyName), StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
    }

    private static string ToCamelCasePath(string propertyPath) =>
        string.IsNullOrEmpty(propertyPath)
            ? propertyPath
            : string.Join('.', propertyPath.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}
