using FluentValidation.TestHelper;
using SmartTask.Application.Common.Models;
using SmartTask.Application.DTOs.Projects;
using SmartTask.Application.Validators.Projects;
using SmartTask.Domain.Constants;

namespace SmartTask.Application.UnitTests.Validators.Projects;

public sealed class UpdateProjectDtoValidatorTests
{
    private readonly UpdateProjectDtoValidator _validator = new();

    private static UpdateProjectDto Valid() => new()
    {
        Name = "Website relaunch",
        StartDate = TestData.Today,
        EndDate = TestData.Today.AddDays(30),
    };

    [Fact]
    public void Validate_ValidDto_HasNoErrors() =>
        _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_StartDateInThePast_IsAllowed() =>
        // A project that has already started must stay editable.
        _validator.TestValidate(Valid() with { StartDate = TestData.Today.AddYears(-1) }).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_EndDateBeforeStartDate_HasError() =>
        _validator.TestValidate(Valid() with { EndDate = TestData.Today.AddDays(-1) })
            .ShouldHaveValidationErrorFor(x => x.EndDate);

    [Theory]
    [InlineData(ProjectConstraints.NameMinLength - 1, false)]
    [InlineData(ProjectConstraints.NameMaxLength + 1, false)]
    [InlineData(ProjectConstraints.NameMinLength, true)]
    public void Validate_NameLength_SharesCreateRules(int length, bool valid)
    {
        var result = _validator.TestValidate(Valid() with { Name = TestData.Text(length) });

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Name);
        else result.ShouldHaveValidationErrorFor(x => x.Name);
    }
}
