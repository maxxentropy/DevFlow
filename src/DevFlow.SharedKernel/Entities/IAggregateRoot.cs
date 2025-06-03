using DevFlow.SharedKernel.Events;

namespace DevFlow.SharedKernel.Entities;

/// <summary>
/// Interface for aggregate roots in the domain.
/// </summary>
public interface IAggregateRoot
{
  /// <summary>
  /// Gets the domain events that have been raised by this aggregate root.
  /// </summary>
  IReadOnlyList<IDomainEvent> DomainEvents { get; }

  /// <summary>
  /// Clears all domain events from this aggregate root.
  /// </summary>
  void ClearDomainEvents();

  /// <summary>
  /// Gets the version of this aggregate for optimistic concurrency control.
  /// </summary>
  int Version { get; }
}