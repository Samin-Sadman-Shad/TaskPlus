using FluentValidation;
using SmartTask.Application.Common.Models;
using SmartTask.Domain.Constants;

namespace SmartTask.Application.Validators.Common;

/// <summary>Paging, search and sorting rules shared by every list query's validator.</summary>
public abstract class PageQueryValidator<T> : AbstractValidator<T>
    where T : PageQuery
{
    private const int FirstPage = 1;
    private const int MinPageSize = 1;

    protected PageQueryValidator(IReadOnlySet<string> allowedSortFields)
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(FirstPage)
            .WithMessage("{PropertyName} must be at least {ComparisonValue}.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(MinPageSize, PagingConstraints.MaxPageSize)
            .WithMessage("{PropertyName} must be between {From} and {To}.");

        RuleFor(x => x.Search)
            .TrimmedMaxLength(PagingConstraints.SearchTermMaxLength);

        RuleFor(x => x.SortBy)
            .ValidSortField(allowedSortFields);

        RuleFor(x => x.SortDirection)
            .IsInEnum()
            .WithMessage("{PropertyName} must be 'Asc' or 'Desc'.");
    }
}
