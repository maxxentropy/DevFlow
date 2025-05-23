using System.Collections.Concurrent;
using MediatR;

namespace DevFlow.SharedKernel.Common;

/// <summary>
/// Base class for aggregate roots in the domain model.
/// Provides domain event management and strong-typed identity.
/// </summary>
/// <typeparam name="TId">The type of the aggregate's identifier</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot
    where TId : class, IEntityId
{
    private readonly ConcurrentQueue<INotification> _domainEvents = new();

    protected AggregateRoot(TId id) : base(id)
    {
    }

    /// <summary>
    /// Gets all domain events that have been raised by this aggregate.
    /// </summary>
    public IReadOnlyCollection<INotification> DomainEvents => _domainEvents.ToArray();

    /// <summary>
    /// Raises a domain event for this aggregate.
    /// </summary>
    /// <param name="domainEvent">The domain event to raise</param>
    protected void RaiseDomainEvent(INotification domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Enqueue(domainEvent);
    }

    /// <summary>
    /// Clears all pending domain events.
    /// </summary>
    public void ClearDomainEvents()
    {
        while (_domainEvents.TryDequeue(out _)) { }
    }
}