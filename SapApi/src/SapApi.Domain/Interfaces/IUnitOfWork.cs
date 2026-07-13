namespace SapApi.Domain.Interfaces;

public interface IUnitOfWork : IAsyncDisposable
{
    IRepository<T> Repository<T>() where T : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs database work inside a transaction. Nesting is supported: only the outermost call commits/rolls back.
    /// Do not perform external SAP HTTP calls inside <paramref name="action"/> — call SAP first, then persist.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
}
