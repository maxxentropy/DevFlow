using DevFlow.Domain.Common;
using DevFlow.Domain.Workflows.Entities;
using DevFlow.Domain.Plugins.Entities;
using DevFlow.Infrastructure.Persistence.Configurations;
using DevFlow.SharedKernel.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DevFlow.Infrastructure.Persistence;

/// <summary>
/// Entity Framework database context for DevFlow.
/// </summary>
public sealed class DevFlowDbContext : DbContext
{
  private readonly IMediator _mediator;
  private readonly ILogger<DevFlowDbContext> _logger;

  public DevFlowDbContext(
      DbContextOptions<DevFlowDbContext> options,
      IMediator mediator,
      ILogger<DevFlowDbContext> logger) : base(options)
  {
    _mediator = mediator;
    _logger = logger;
  }

  /// <summary>
  /// Gets or sets the workflows DbSet.
  /// </summary>
  public DbSet<Workflow> Workflows => Set<Workflow>();

  /// <summary>
  /// Gets or sets the workflow steps DbSet.
  /// </summary>
  public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();

  /// <summary>
  /// Gets or sets the plugins DbSet.
  /// </summary>
  public DbSet<Plugin> Plugins => Set<Plugin>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    // Apply entity configurations
    modelBuilder.ApplyConfiguration(new WorkflowConfiguration());
    modelBuilder.ApplyConfiguration(new WorkflowStepConfiguration());
    modelBuilder.ApplyConfiguration(new PluginConfiguration());
  }

  /// <summary>
  /// Saves changes and publishes domain events.
  /// </summary>
  public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      // Get all aggregate roots with domain events
      var aggregatesWithEvents = ChangeTracker.Entries<IAggregateRoot>()
          .Where(e => e.Entity.DomainEvents.Count != 0)
          .Select(e => e.Entity)
          .ToList();

      // Save changes first
      var result = await base.SaveChangesAsync(cancellationToken);

      // Then publish domain events
      await PublishDomainEventsAsync(aggregatesWithEvents, cancellationToken);

      return result;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error occurred while saving changes to database");
      throw;
    }
  }

  private async Task PublishDomainEventsAsync(IEnumerable<IAggregateRoot> aggregates, CancellationToken cancellationToken)
  {
    var domainEvents = aggregates
        .SelectMany(aggregate => aggregate.DomainEvents)
        .ToList();

    // Clear events from aggregates first
    foreach (var aggregate in aggregates)
    {
      aggregate.ClearDomainEvents();
    }

    // Publish events
    foreach (var domainEvent in domainEvents)
    {
      try
      {
        await _mediator.Publish(domainEvent, cancellationToken);
        _logger.LogDebug("Published domain event: {EventType}", domainEvent.GetType().Name);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error publishing domain event: {EventType}", domainEvent.GetType().Name);
        // Don't rethrow - we don't want domain event publishing to break the transaction
      }
    }

    if (domainEvents.Count > 0)
    {
      _logger.LogInformation("Published {Count} domain events", domainEvents.Count);
    }
  }
}