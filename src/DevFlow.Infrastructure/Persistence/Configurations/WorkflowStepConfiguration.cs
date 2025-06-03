using DevFlow.Domain.Common;
using DevFlow.Domain.Workflows.Entities;
using DevFlow.Domain.Workflows.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace DevFlow.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for WorkflowStep entity.
/// </summary>
public sealed class WorkflowStepConfiguration : IEntityTypeConfiguration<WorkflowStep>
{
  public void Configure(EntityTypeBuilder<WorkflowStep> builder)
  {
    // Table configuration
    builder.ToTable("WorkflowSteps");

    // Primary key
    builder.HasKey(s => s.Id);

    // Configure WorkflowStepId
    builder.Property(s => s.Id)
        .HasConversion(
            id => id.Value,
            value => WorkflowStepId.From(value))
        .HasMaxLength(50)
        .IsRequired();

    // Configure properties
    builder.Property(s => s.Name)
        .HasMaxLength(200)
        .IsRequired();

    builder.Property(s => s.PluginId)
        .HasConversion(
            id => id.Value,
            value => PluginId.From(value))
        .HasMaxLength(50)
        .IsRequired();

    builder.Property(s => s.Order)
        .IsRequired();

    // Configure Configuration as JSON
    builder.Property(s => s.Configuration)
        .HasConversion(
            v => JsonSerializer.Serialize(v, JsonValueComparerHelper._jsonOptions),
            v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, JsonValueComparerHelper._jsonOptions) ?? new Dictionary<string, object>())
        .HasColumnType("TEXT")
        .Metadata.SetValueComparer(
            JsonValueComparerHelper.CreateJsonValueComparer<Dictionary<string, object>>());

    builder.Property(s => s.Status)
        .HasConversion<string>()
        .HasMaxLength(20)
        .IsRequired();

    builder.Property(s => s.CreatedAt)
        .IsRequired();

    builder.Property(s => s.StartedAt);

    builder.Property(s => s.CompletedAt);

    builder.Property(s => s.ErrorMessage)
        .HasMaxLength(2000);

    builder.Property(s => s.Output)
        .HasMaxLength(10000);

    // Configure relationship with Workflow
    builder.HasOne<Workflow>()
        .WithMany(w => w.Steps)
        .HasForeignKey("WorkflowId")
        .IsRequired();

    // Add foreign key for WorkflowId (shadow property with proper conversion)
    builder.Property<WorkflowId>("WorkflowId")
        .HasConversion(
            id => id.Value,
            value => WorkflowId.From(value))
        .HasMaxLength(50)
        .IsRequired();

    // Indexes
    builder.HasIndex("WorkflowId", nameof(WorkflowStep.Order))
        .HasDatabaseName("IX_WorkflowSteps_WorkflowId_Order");

    builder.HasIndex(s => s.Status)
        .HasDatabaseName("IX_WorkflowSteps_Status");
  }
}