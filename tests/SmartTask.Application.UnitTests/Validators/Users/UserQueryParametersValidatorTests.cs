using FluentValidation.TestHelper;
using SmartTask.Application.Common.Models;
using SmartTask.Application.DTOs.Users;
using SmartTask.Application.Validators.Users;
using SmartTask.Domain.Constants;
using SmartTask.Domain.Enums;

namespace SmartTask.Application.UnitTests.Validators.Users;

public sealed class UserQueryParametersValidatorTests
{
    private readonly UserQueryParametersValidator _validator = new();

    [Fact]
    public void Validate_Defaults_HasNoErrors() =>
        _validator.TestValidate(new UserQueryParameters()).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("name")]
    [InlineData("EMAIL")]
    [InlineData("createdAt")]
    public void Validate_AllowedSortField_HasNoErrors(string sortBy) =>
        _validator.TestValidate(new UserQueryParameters { SortBy = sortBy })
            .ShouldNotHaveValidationErrorFor(x => x.SortBy);

    [Theory]
    [InlineData("passwordHash")]
    [InlineData("name; DROP TABLE Users")]
    public void Validate_UnknownSortField_HasError(string sortBy) =>
        _validator.TestValidate(new UserQueryParameters { SortBy = sortBy })
            .ShouldHaveValidationErrorFor(x => x.SortBy)
            .WithErrorMessage("Sort By must be one of: createdAt, email, name.");

    [Fact]
    public void Validate_UndefinedRoleFilter_HasError() =>
        _validator.TestValidate(new UserQueryParameters { Role = (UserRole)42 })
            .ShouldHaveValidationErrorFor(x => x.Role);
}
