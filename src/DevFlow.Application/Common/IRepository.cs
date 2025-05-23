using DevFlow.SharedKernel.Common;

namespace DevFlow.Application.Common;

/// <summary>
/// Base interface for repositories.
/// </summary>
/// <typeparam name="TEntity">The entity type</typeparam>
/// <typeparam name="TId">The entity identifier type</typeparam>
public interface IRepository<TEntity, in TId>
    where TEntity : class, IAggregateRoot
    where TId : class, IEntityId
{
    /// <summary>
    /// Gets an entity by its identifier.
    /// </summary>
    /// <param name="id">The entity identifier</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The entity if found, otherwise null</returns>
    Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new entity to the repository.
    /// </summary>
    /// <param name="entity">The entity to add</param>
    /// <param name="cancellationToken">The cancellation token</param>
    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing entity in the repository.
    /// </summary>
    /// <param name="entity">The entity to update</param>
    /// <param name="cancellationToken">The cancellation token</param>
    Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an entity from the repository.
    /// </summary>
    /// <param name="entity">The entity to remove</param>
    /// <param name="cancellationToken">The cancellation token</param>
    Task RemoveAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves all pending changes to the underlying data store.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}