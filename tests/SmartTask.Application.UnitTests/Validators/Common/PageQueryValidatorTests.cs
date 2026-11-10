using FluentValidation.TestHelper;
using SmartTask.Application.Common.Models;
using SortDirection = SmartTask.Application.Common.Models.SortDirection;
using SmartTask.Application.Validators.Projects;
using SmartTask.Domain.Constants;

namespace SmartTask.Application.UnitTests.Validators.Common;

/// <summary>The shared paging rules, exercised through <see cref="ProjectQueryParametersValidator"/>.</summary>
public sealed class PageQueryValidatorTests
{
    private readonly ProjectQueryParametersValidator _validator = new();

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    public void Validate_PageNumber_MustBeAtLeastOne(int pageNumber, bool valid)
    {
        var result = _validator.TestValidate(new ProjectQueryParameters { PageNumber = pageNumber });

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.PageNumber);
        else result.ShouldHaveValidationErrorFor(x => x.PageNumber);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(PagingConstraints.MaxPageSize, true)]
    [InlineData(PagingConstraints.MaxPageSize + 1, false)]
    public void Validate_PageSize_RespectsBounds(int pageSize, bool valid)
    {
        var result = _validator.TestValidate(new ProjectQueryParameters { PageSize = pageSize });

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.PageSize);
        else result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Theory]
    [InlineData(PagingConstraints.SearchTermMaxLength, true)]
    [InlineData(PagingConstraints.SearchTermMaxLength + 1, false)]
    public void Validate_SearchLength_RespectsMaximum(int length, bool valid)
    {
        var result = _validator.TestValidate(new ProjectQueryParameters { Search = TestData.Text(length) });

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Search);
        else result.ShouldHaveValidationErrorFor(x => x.Search);
    }

    [Fact]
    public void Validate_UndefinedSortDirection_HasError() =>
        _validator.TestValidate(new ProjectQueryParameters { SortDirection = (SortDirection)7 })
            .ShouldHaveValidationErrorFor(x => x.SortDirection);

    [Theory]
    [InlineData(SortDirection.Asc)]
    [InlineData(SortDirection.Desc)]
    [InlineData(null)]
    public void Validate_ValidSortDirection_HasNoErrors(SortDirection? direction) =>
        _validator.TestValidate(new ProjectQueryParameters { SortDirection = direction })
            .ShouldNotHaveValidationErrorFor(x => x.SortDirection);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" startDate ")]
    public void Validate_EmptyOrPaddedAllowedSortBy_HasNoErrors(string? sortBy) =>
        _validator.TestValidate(new ProjectQueryParameters { SortBy = sortBy })
            .ShouldNotHaveValidationErrorFor(x => x.SortBy);
}
