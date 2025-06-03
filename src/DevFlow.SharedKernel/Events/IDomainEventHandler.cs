using MediatR;

namespace DevFlow.SharedKernel.Events;

/// <summary>
/// Interface for domain event handlers.
/// </summary>
/// <typeparam name="TDomainEvent">The type of domain event to handle</typeparam>
public interface IDomainEventHandler<in TDomainEvent> : INotificationHandler<TDomainEvent>
    where TDomainEvent : IDomainEvent
{
}