using FluentValidation.TestHelper;
using SmartTask.Application.Common.Models;
using SmartTask.Application.DTOs.Projects;
using SmartTask.Application.Validators.Projects;
using SmartTask.Domain.Constants;

namespace SmartTask.Application.UnitTests.Validators.Projects;

public sealed class CreateProjectDtoValidatorTests
{
    private readonly CreateProjectDtoValidator _validator = new(TestData.Clock());

    private static CreateProjectDto Valid() => new()
    {
        Name = "Website relaunch",
        Description = "Redesign and relaunch.",
        StartDate = TestData.Today,
        EndDate = TestData.Today.AddDays(30),
    };

    [Fact]
    public void Validate_ValidDto_HasNoErrors() =>
        _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData(ProjectConstraints.NameMinLength - 1, false)]
    [InlineData(ProjectConstraints.NameMinLength, true)]
    [InlineData(ProjectConstraints.NameMaxLength, true)]
    [InlineData(ProjectConstraints.NameMaxLength + 1, false)]
    public void Validate_NameLength_RespectsBounds(int length, bool valid)
    {
        var result = _validator.TestValidate(Valid() with { Name = TestData.Text(length) });

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Name);
        else result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    public void Validate_BlankName_HasRequiredError(string name) =>
        _validator.TestValidate(Valid() with { Name = name })
            .ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Name is required.");

    [Fact]
    public void Validate_NamePaddedBelowMinimum_HasError() =>
        // "  ab  " is 6 characters but only 2 after trimming, which is what the domain stores.
        _validator.TestValidate(Valid() with { Name = "  ab  " })
            .ShouldHaveValidationErrorFor(x => x.Name);

    [Theory]
    [InlineData(ProjectConstraints.DescriptionMaxLength, true)]
    [InlineData(ProjectConstraints.DescriptionMaxLength + 1, false)]
    public void Validate_DescriptionLength_RespectsMaximum(int length, bool valid)
    {
        var result = _validator.TestValidate(Valid() with { Description = TestData.Text(length) });

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Description);
        else result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_NullDescription_HasNoErrors() =>
        _validator.TestValidate(Valid() with { Description = null }).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_MissingStartDate_HasOnlyRequiredError()
    {
        var result = _validator.TestValidate(Valid() with { StartDate = default, EndDate = null });

        result.ShouldHaveValidationErrorFor(x => x.StartDate).WithErrorMessage("Start Date is required.");
        result.Errors.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(-1, true)]   // tolerance for clients whose local date is behind UTC
    [InlineData(-2, false)]
    [InlineData(0, true)]
    [InlineData(365, true)]
    public void Validate_StartDateRelativeToToday_AllowsOneDayOfTolerance(int offsetDays, bool valid)
    {
        var start = TestData.Today.AddDays(offsetDays);
        var result = _validator.TestValidate(Valid() with { StartDate = start, EndDate = null });

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.StartDate);
        else result.ShouldHaveValidationErrorFor(x => x.StartDate);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public void Validate_EndDateRelativeToStartDate_MustBeOnOrAfter(int offsetDays, bool valid)
    {
        var result = _validator.TestValidate(Valid() with { EndDate = TestData.Today.AddDays(offsetDays) });

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.EndDate);
        else result.ShouldHaveValidationErrorFor(x => x.EndDate).WithErrorMessage("End date must be on or after the start date.");
    }

    [Fact]
    public void Validate_UsesClockAtValidationTime()
    {
        var clock = TestData.Clock();
        var validator = new CreateProjectDtoValidator(clock);
        var dto = Valid() with { StartDate = TestData.Today, EndDate = null };

        validator.TestValidate(dto).ShouldNotHaveAnyValidationErrors();

        clock.Advance(TimeSpan.FromDays(2));
        validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.StartDate);
    }
}
