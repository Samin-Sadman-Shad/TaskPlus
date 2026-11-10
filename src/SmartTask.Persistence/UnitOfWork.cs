using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartTask.Application.Common.Exceptions;
using SmartTask.Application.Contracts.Persistence;

namespace SmartTask.Persistence;

internal sealed class UnitOfWork(SmartTaskDbContext context) : IUnitOfWork
{
    /// <summary>SQL Server errors for a duplicate key in a unique index (2601) or a unique constraint (2627).</summary>
    private static readonly HashSet<int> UniqueViolationErrorNumbers = [2601, 2627];

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflictException("The record was changed by someone else. Reload it and try again.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && UniqueViolationErrorNumbers.Contains(sql.Number))
        {
            throw new ConflictException("A record with the same unique value already exists.", ex);
        }
    }

    /// <inheritdoc />
    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        var strategy = context.Database.CreateExecutionStrategy();

        return strategy.ExecuteAsync(
            async ct =>
            {
                await using var transaction = await context.Database.BeginTransactionAsync(ct);
                await action(ct);
                await transaction.CommitAsync(ct);
            },
            cancellationToken);
    }
}
