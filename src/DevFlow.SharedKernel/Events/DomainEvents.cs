namespace DevFlow.SharedKernel.Events;

/// <summary>
/// Base class for domain events.
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
  /// <summary>
  /// Gets the unique identifier for this domain event.
  /// </summary>
  public Guid Id { get; } = Guid.NewGuid();

  /// <summary>
  /// Gets the date and time when the event occurred.
  /// </summary>
  public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}