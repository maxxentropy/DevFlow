using MediatR;

namespace DevFlow.SharedKernel.Common;

/// <summary>
/// Marker interface for aggregate roots.
/// Indicates that this entity is the root of an aggregate boundary.
/// </summary>
public interface IAggregateRoot
{
    /// <summary>
    /// Gets all domain events that have been raised by this aggregate.
    /// </summary>
    IReadOnlyCollection<INotification> DomainEvents { get; }

    /// <summary>
    /// Clears all pending domain events.
    /// </summary>
    void ClearDomainEvents();
}