using MediatR;

namespace DevFlow.SharedKernel.Events;

/// <summary>
/// Marker interface for domain events.
/// </summary>
public interface IDomainEvent : INotification
{
  /// <summary>
  /// Gets the unique identifier for this domain event.
  /// </summary>
  Guid Id { get; }

  /// <summary>
  /// Gets the date and time when the event occurred.
  /// </summary>
  DateTimeOffset OccurredAt { get; }
}