using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SapApi.Domain.Interfaces;

namespace SapApi.Infrastructure.Persistence;

public class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    private readonly Dictionary<Type, object> _repositories = [];
    private IDbContextTransaction? _transaction;
    private int _transactionDepth;

    public IRepository<T> Repository<T>() where T : class
    {
        var type = typeof(T);
        if (!_repositories.TryGetValue(type, out var repo))
        {
            repo = new Repository<T>(context);
            _repositories[type] = repo;
        }
        return (IRepository<T>)repo;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            _transactionDepth++;
            return;
        }

        // Prefer ExecuteInTransactionAsync — NpgsqlRetryingExecutionStrategy requires
        // user transactions to run inside CreateExecutionStrategy().
        _transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        _transactionDepth = 1;
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null) return;

        if (_transactionDepth > 1)
        {
            _transactionDepth--;
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        await context.SaveChangesAsync(cancellationToken);
        await _transaction.CommitAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
        _transactionDepth = 0;
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null) return;

        await _transaction.RollbackAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
        _transactionDepth = 0;
    }

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        // Nested call: reuse the ambient transaction started by the outer strategy.
        if (_transaction != null)
        {
            _transactionDepth++;
            try
            {
                await action(cancellationToken);
                await context.SaveChangesAsync(cancellationToken);
                _transactionDepth--;
            }
            catch
            {
                // Outer ExecuteInTransaction owns commit/rollback of the real transaction.
                _transactionDepth = 0;
                throw;
            }

            return;
        }

        var strategy = context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            _transaction = transaction;
            _transactionDepth = 1;
            try
            {
                await action(cancellationToken);
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
            finally
            {
                _transaction = null;
                _transactionDepth = 0;
            }
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction != null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
            _transactionDepth = 0;
        }
    }
}
