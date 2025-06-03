using DevFlow.Domain.Common;
using DevFlow.Domain.Workflows.Entities;
using DevFlow.Domain.Workflows.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevFlow.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for Workflow entity.
/// </summary>
public sealed class WorkflowConfiguration : IEntityTypeConfiguration<Workflow>
{
  public void Configure(EntityTypeBuilder<Workflow> builder)
  {
    // Table configuration
    builder.ToTable("Workflows");

    // Primary key
    builder.HasKey(w => w.Id);

    // Configure WorkflowId
    builder.Property(w => w.Id)
        .HasConversion(
            id => id.Value,
            value => WorkflowId.From(value))
        .HasMaxLength(50)
        .IsRequired();

    // Configure WorkflowName value object
    builder.OwnsOne(w => w.Name, nameBuilder =>
    {
      nameBuilder.Property(n => n.Value)
          .HasColumnName("Name")
          .HasMaxLength(100)
          .IsRequired();
    });

    // Configure WorkflowDescription value object
    builder.OwnsOne(w => w.Description, descBuilder =>
    {
      descBuilder.Property(d => d.Value)
          .HasColumnName("Description")
          .HasMaxLength(1000);
    });

    // Configure properties
    builder.Property(w => w.Status)
        .HasConversion<string>()
        .HasMaxLength(20)
        .IsRequired();

    builder.Property(w => w.CreatedAt)
        .IsRequired();

    builder.Property(w => w.UpdatedAt)
        .IsRequired();

    builder.Property(w => w.StartedAt);

    builder.Property(w => w.CompletedAt);

    builder.Property(w => w.ErrorMessage)
        .HasMaxLength(2000);

    // Configure relationships
    builder.HasMany(w => w.Steps)
        .WithOne()
        .HasForeignKey("WorkflowId")
        .OnDelete(DeleteBehavior.Cascade);

    // Indexes
    builder.HasIndex(w => w.Status)
        .HasDatabaseName("IX_Workflows_Status");

    builder.HasIndex(w => w.CreatedAt)
        .HasDatabaseName("IX_Workflows_CreatedAt");

    // Ignore domain events (not persisted)
    builder.Ignore(w => w.DomainEvents);
  }
}