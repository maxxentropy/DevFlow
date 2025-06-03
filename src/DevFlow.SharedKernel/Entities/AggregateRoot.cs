using DevFlow.SharedKernel.Events;
using DevFlow.SharedKernel.ValueObjects;

namespace DevFlow.SharedKernel.Entities;

/// <summary>
/// Base class for aggregate roots in the domain.
/// </summary>
/// <typeparam name="TId">The type of the aggregate root identifier</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot
    where TId : EntityId
{
  private readonly List<IDomainEvent> _domainEvents = new();

  /// <summary>
  /// Gets the domain events that have been raised by this aggregate root.
  /// </summary>
  public new IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

  /// <summary>
  /// Adds a domain event to this aggregate root.
  /// </summary>
  /// <param name="domainEvent">The domain event to add</param>
  protected void AddDomainEvent(IDomainEvent domainEvent)
  {
    _domainEvents.Add(domainEvent);
    base.AddDomainEvent(domainEvent);
  }

  /// <summary>
  /// Clears all domain events from this aggregate root.
  /// </summary>
  public new void ClearDomainEvents()
  {
    _domainEvents.Clear();
    base.ClearDomainEvents();
  }

  /// <summary>
  /// Gets the version of this aggregate for optimistic concurrency control.
  /// </summary>
  public int Version { get; protected set; }

  /// <summary>
  /// Increments the version of this aggregate.
  /// </summary>
  protected void IncrementVersion()
  {
    Version++;
  }
}

/// <summary>
/// Base class for aggregate roots with Guid identifiers.
/// </summary>
public abstract class AggregateRoot : AggregateRoot<EntityId<Guid>>
{
  protected AggregateRoot()
  {
    Id = EntityId<Guid>.New();
  }

  protected AggregateRoot(Guid id)
  {
    Id = EntityId<Guid>.From(id);
  }
}