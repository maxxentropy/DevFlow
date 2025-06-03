using DevFlow.SharedKernel.Entities;
using DevFlow.SharedKernel.ValueObjects;
using System.Linq.Expressions;

namespace DevFlow.SharedKernel.Interfaces;

/// <summary>
/// Generic repository interface for domain entities.
/// </summary>
/// <typeparam name="TEntity">The entity type</typeparam>
/// <typeparam name="TId">The entity identifier type</typeparam>
public interface IRepository<TEntity, TId>
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
  /// Gets all entities.
  /// </summary>
  /// <param name="cancellationToken">The cancellation token</param>
  /// <returns>All entities</returns>
  Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);

  /// <summary>
  /// Finds entities that match the specified predicate.
  /// </summary>
  /// <param name="predicate">The predicate to match</param>
  /// <param name="cancellationToken">The cancellation token</param>
  /// <returns>Entities that match the predicate</returns>
  Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

  /// <summary>
  /// Gets a single entity that matches the specified predicate.
  /// </summary>
  /// <param name="predicate">The predicate to match</param>
  /// <param name="cancellationToken">The cancellation token</param>
  /// <returns>The entity if found, otherwise null</returns>
  Task<TEntity?> SingleOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

  /// <summary>
  /// Checks if any entity matches the specified predicate.
  /// </summary>
  /// <param name="predicate">The predicate to match</param>
  /// <param name="cancellationToken">The cancellation token</param>
  /// <returns>True if any entity matches, otherwise false</returns>
  Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

  /// <summary>
  /// Gets the count of entities that match the specified predicate.
  /// </summary>
  /// <param name="predicate">The predicate to match</param>
  /// <param name="cancellationToken">The cancellation token</param>
  /// <returns>The count of matching entities</returns>
  Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);

  /// <summary>
  /// Adds an entity to the repository.
  /// </summary>
  /// <param name="entity">The entity to add</param>
  void Add(TEntity entity);

  /// <summary>
  /// Adds multiple entities to the repository.
  /// </summary>
  /// <param name="entities">The entities to add</param>
  void AddRange(IEnumerable<TEntity> entities);

  /// <summary>
  /// Updates an entity in the repository.
  /// </summary>
  /// <param name="entity">The entity to update</param>
  void Update(TEntity entity);

  /// <summary>
  /// Updates multiple entities in the repository.
  /// </summary>
  /// <param name="entities">The entities to update</param>
  void UpdateRange(IEnumerable<TEntity> entities);

  /// <summary>
  /// Removes an entity from the repository.
  /// </summary>
  /// <param name="entity">The entity to remove</param>
  void Remove(TEntity entity);

  /// <summary>
  /// Removes multiple entities from the repository.
  /// </summary>
  /// <param name="entities">The entities to remove</param>
  void RemoveRange(IEnumerable<TEntity> entities);

  /// <summary>
  /// Removes an entity by its identifier.
  /// </summary>
  /// <param name="id">The entity identifier</param>
  /// <param name="cancellationToken">The cancellation token</param>
  /// <returns>True if the entity was removed, otherwise false</returns>
  Task<bool> RemoveByIdAsync(TId id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-only repository interface for domain entities.
/// </summary>
/// <typeparam name="TEntity">The entity type</typeparam>
/// <typeparam name="TId">The entity identifier type</typeparam>
public interface IReadOnlyRepository<TEntity, TId>
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
  /// Gets all entities.
  /// </summary>
  /// <param name="cancellationToken">The cancellation token</param>
  /// <returns>All entities</returns>
  Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);

  /// <summary>
  /// Finds entities that match the specified predicate.
  /// </summary>
  /// <param name="predicate">The predicate to match</param>
  /// <param name="cancellationToken">The cancellation token</param>
  /// <returns>Entities that match the predicate</returns>
  Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

  /// <summary>
  /// Gets a single entity that matches the specified predicate.
  /// </summary>
  /// <param name="predicate">The predicate to match</param>
  /// <param name="cancellationToken">The cancellation token</param>
  /// <returns>The entity if found, otherwise null</returns>
  Task<TEntity?> SingleOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

  /// <summary>
  /// Checks if any entity matches the specified predicate.
  /// </summary>
  /// <param name="predicate">The predicate to match</param>
  /// <param name="cancellationToken">The cancellation token</param>
  /// <returns>True if any entity matches, otherwise false</returns>
  Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

  /// <summary>
  /// Gets the count of entities that match the specified predicate.
  /// </summary>
  /// <param name="predicate">The predicate to match</param>
  /// <param name="cancellationToken">The cancellation token</param>
  /// <returns>The count of matching entities</returns>
  Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);
}