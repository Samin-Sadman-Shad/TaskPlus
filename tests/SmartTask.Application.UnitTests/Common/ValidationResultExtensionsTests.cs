using FluentValidation.Results;
using SmartTask.Application.Common.Validation;

namespace SmartTask.Application.UnitTests.Common;

public sealed class ValidationResultExtensionsTests
{
    [Fact]
    public void ToErrorDictionary_GroupsByPropertyWithCamelCaseKeys()
    {
        var result = new ValidationResult(
        [
            new ValidationFailure("EndDate", "End date must be on or after the start date."),
            new ValidationFailure("Password", "Password is too short."),
            new ValidationFailure("Password", "Password needs a digit."),
            new ValidationFailure("Members[0].UserId", "User Id is required."),
        ]);

        var errors = result.ToErrorDictionary();

        errors.Keys.ShouldBe(["endDate", "password", "members[0].userId"], ignoreOrder: true);
        errors["password"].ShouldBe(["Password is too short.", "Password needs a digit."]);
        errors["endDate"].ShouldHaveSingleItem();
    }

    [Fact]
    public void ToErrorDictionary_DuplicateMessages_AreReportedOnce()
    {
        var result = new ValidationResult(
        [
            new ValidationFailure("Name", "Name is required."),
            new ValidationFailure("Name", "Name is required."),
        ]);

        result.ToErrorDictionary()["name"].ShouldBe(["Name is required."]);
    }

    [Fact]
    public void ToErrorDictionary_NoErrors_IsEmpty() =>
        new ValidationResult().ToErrorDictionary().ShouldBeEmpty();
}
