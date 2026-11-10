using FluentValidation.TestHelper;
using SmartTask.Application.Common.Models;
using SortDirection = SmartTask.Application.Common.Models.SortDirection;
using SmartTask.Application.DTOs.Tasks;
using SmartTask.Application.Validators.Tasks;
using SmartTask.Domain.Constants;
using SmartTask.Domain.Enums;

namespace SmartTask.Application.UnitTests.Validators.Tasks;

public sealed class TaskQueryParametersValidatorTests
{
    private readonly TaskQueryParametersValidator _validator = new();

    [Fact]
    public void Validate_FullyPopulated_HasNoErrors() =>
        _validator.TestValidate(new TaskQueryParameters
        {
            ProjectId = Guid.NewGuid(),
            Status = TaskItemStatus.InProgress,
            Priority = TaskPriority.Critical,
            AssigneeId = Guid.NewGuid(),
            DueDateFrom = TestData.Today,
            DueDateTo = TestData.Today.AddDays(7),
            IsOverdue = false,
            Search = "docs",
            SortBy = "dueDate",
            SortDirection = SortDirection.Asc,
        }).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("title")]
    [InlineData("DUEDATE")]
    [InlineData("priority")]
    [InlineData("status")]
    [InlineData("createdAt")]
    public void Validate_AllowedSortField_HasNoErrors(string sortBy) =>
        _validator.TestValidate(new TaskQueryParameters { SortBy = sortBy }).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("name; DROP TABLE")]
    [InlineData("assigneeId")]
    public void Validate_UnknownSortField_HasError(string sortBy) =>
        _validator.TestValidate(new TaskQueryParameters { SortBy = sortBy })
            .ShouldHaveValidationErrorFor(x => x.SortBy);

    [Fact]
    public void Validate_UndefinedStatusFilter_HasError() =>
        _validator.TestValidate(new TaskQueryParameters { Status = (TaskItemStatus)9 })
            .ShouldHaveValidationErrorFor(x => x.Status);

    [Fact]
    public void Validate_UndefinedPriorityFilter_HasError() =>
        _validator.TestValidate(new TaskQueryParameters { Priority = (TaskPriority)9 })
            .ShouldHaveValidationErrorFor(x => x.Priority);

    [Fact]
    public void Validate_DueDateRangeReversed_HasError() =>
        _validator.TestValidate(new TaskQueryParameters { DueDateFrom = TestData.Today, DueDateTo = TestData.Today.AddDays(-1) })
            .ShouldHaveValidationErrorFor(x => x.DueDateTo);
}
