namespace DevFlow.SharedKernel.Interfaces;

/// <summary>
/// Interface for unit of work pattern.
/// </summary>
public interface IUnitOfWork : IDisposable
{
  /// <summary>
  /// Saves all changes made in this unit of work.
  /// </summary>
  /// <param name="cancellationToken">The cancellation token</param>
  /// <returns>The number of state entries written to the database</returns>
  Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

  /// <summary>
  /// Begins a new transaction.
  /// </summary>
  /// <param name="cancellationToken">The cancellation token</param>
  /// <returns>The transaction</returns>
  Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for database transactions.
/// </summary>
public interface ITransaction : IDisposable
{
  /// <summary>
  /// Commits the transaction.
  /// </summary>
  /// <param name="cancellationToken">The cancellation token</param>
  Task CommitAsync(CancellationToken cancellationToken = default);

  /// <summary>
  /// Rolls back the transaction.
  /// </summary>
  /// <param name="cancellationToken">The cancellation token</param>
  Task RollbackAsync(CancellationToken cancellationToken = default);
}