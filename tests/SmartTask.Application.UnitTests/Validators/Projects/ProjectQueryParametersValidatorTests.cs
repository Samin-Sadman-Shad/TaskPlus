using FluentValidation.TestHelper;
using SmartTask.Application.Common.Models;
using SmartTask.Application.DTOs.Projects;
using SmartTask.Application.Validators.Projects;
using SmartTask.Domain.Constants;

namespace SmartTask.Application.UnitTests.Validators.Projects;

public sealed class ProjectQueryParametersValidatorTests
{
    private readonly ProjectQueryParametersValidator _validator = new();

    [Theory]
    [InlineData("name")]
    [InlineData("StartDate")]
    [InlineData("endDate")]
    [InlineData("createdAt")]
    public void Validate_AllowedSortField_HasNoErrors(string sortBy) =>
        _validator.TestValidate(new ProjectQueryParameters { SortBy = sortBy }).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("name; DROP TABLE")]
    [InlineData("OwnerId")]
    [InlineData("RowVersion")]
    public void Validate_UnknownSortField_HasError(string sortBy) =>
        _validator.TestValidate(new ProjectQueryParameters { SortBy = sortBy })
            .ShouldHaveValidationErrorFor(x => x.SortBy)
            .WithErrorMessage("Sort By must be one of: createdAt, endDate, name, startDate.");

    [Fact]
    public void Validate_StartDateRangeReversed_HasError() =>
        _validator.TestValidate(new ProjectQueryParameters { StartDateFrom = TestData.Today, StartDateTo = TestData.Today.AddDays(-1) })
            .ShouldHaveValidationErrorFor(x => x.StartDateTo);

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void Validate_StartDateRangeInOrder_HasNoErrors(int lengthDays) =>
        _validator.TestValidate(new ProjectQueryParameters { StartDateFrom = TestData.Today, StartDateTo = TestData.Today.AddDays(lengthDays) })
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_OpenEndedRange_HasNoErrors() =>
        _validator.TestValidate(new ProjectQueryParameters { StartDateTo = TestData.Today }).ShouldNotHaveAnyValidationErrors();
}
