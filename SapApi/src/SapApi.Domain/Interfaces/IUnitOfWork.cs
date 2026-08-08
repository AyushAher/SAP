namespace SapApi.Domain.Interfaces;

public interface IUnitOfWork : IAsyncDisposable
{
    IRepository<T> Repository<T>() where T : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// True when an ambient transaction is open (via <see cref="ExecuteInTransactionAsync"/> nesting
    /// or an explicit begin). Uncommitted inserts are not visible to other readers under PostgreSQL
    /// READ COMMITTED and are removed on rollback.
    /// </summary>
    bool HasActiveTransaction { get; }

    /// <summary>
    /// Runs database work inside a transaction. Nesting is supported: only the outermost call commits/rolls back.
    /// Prefer keeping SAP HTTP outside when possible. Payment/DP create flows may intentionally hold the
    /// transaction across SAP so local rows (and approval drafts) roll back on SAP failure — see
    /// stage-wise payment create. Do not rely on automatic DB retries duplicating SAP posts.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
}
