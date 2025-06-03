using DevFlow.SharedKernel.ValueObjects;

namespace DevFlow.SharedKernel.Entities;

/// <summary>
/// Base class for all domain entities.
/// </summary>
/// <typeparam name="TId">The type of the entity identifier</typeparam>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : EntityId
{
  private readonly List<object> _domainEvents = new();

  /// <summary>
  /// Gets the entity identifier.
  /// </summary>
  public TId Id { get; protected set; } = default!;

  /// <summary>
  /// Gets the domain events that have been raised by this entity.
  /// </summary>
  public IReadOnlyList<object> DomainEvents => _domainEvents.AsReadOnly();

  /// <summary>
  /// Adds a domain event to this entity.
  /// </summary>
  /// <param name="domainEvent">The domain event to add</param>
  protected void AddDomainEvent(object domainEvent)
  {
    _domainEvents.Add(domainEvent);
  }

  /// <summary>
  /// Clears all domain events from this entity.
  /// </summary>
  public void ClearDomainEvents()
  {
    _domainEvents.Clear();
  }

  /// <summary>
  /// Determines whether two entities are equal.
  /// </summary>
  public bool Equals(Entity<TId>? other)
  {
    if (other is null) return false;
    if (ReferenceEquals(this, other)) return true;
    if (GetType() != other.GetType()) return false;

    return Id.Equals(other.Id);
  }

  /// <summary>
  /// Determines whether the specified object is equal to the current entity.
  /// </summary>
  public override bool Equals(object? obj)
  {
    return obj is Entity<TId> entity && Equals(entity);
  }

  /// <summary>
  /// Returns the hash code for this entity.
  /// </summary>
  public override int GetHashCode()
  {
    return Id.GetHashCode();
  }

  /// <summary>
  /// Equality operator.
  /// </summary>
  public static bool operator ==(Entity<TId>? left, Entity<TId>? right)
  {
    return left?.Equals(right) ?? right is null;
  }

  /// <summary>
  /// Inequality operator.
  /// </summary>
  public static bool operator !=(Entity<TId>? left, Entity<TId>? right)
  {
    return !(left == right);
  }
}

/// <summary>
/// Base class for entities with Guid identifiers.
/// </summary>
public abstract class Entity : Entity<EntityId<Guid>>
{
  protected Entity()
  {
    Id = EntityId<Guid>.New();
  }

  protected Entity(Guid id)
  {
    Id = EntityId<Guid>.From(id);
  }
}