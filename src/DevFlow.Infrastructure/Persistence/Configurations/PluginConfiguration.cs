using DevFlow.Domain.Common;
using DevFlow.Domain.Plugins.Entities;
using DevFlow.Domain.Plugins.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace DevFlow.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for Plugin entity.
/// </summary>
public sealed class PluginConfiguration : IEntityTypeConfiguration<Plugin>
{
  public void Configure(EntityTypeBuilder<Plugin> builder)
  {
    // Table configuration
    builder.ToTable("Plugins");

    // Primary key
    builder.HasKey(p => p.Id);

    // Configure PluginId
    builder.Property(p => p.Id)
        .HasConversion(
            id => id.Value,
            value => PluginId.From(value))
        .HasMaxLength(50)
        .IsRequired();

    // Configure PluginMetadata value object
    builder.OwnsOne(p => p.Metadata, metaBuilder =>
    {
      metaBuilder.Property(m => m.Name)
          .HasColumnName("Name")
          .HasMaxLength(100)
          .IsRequired();

      metaBuilder.Property(m => m.Version)
          .HasColumnName("Version")
          .HasConversion(
              v => v.ToString(),
              v => Version.Parse(v))
          .HasMaxLength(20)
          .IsRequired();

      metaBuilder.Property(m => m.Description)
          .HasColumnName("Description")
          .HasMaxLength(1000);

      metaBuilder.Property(m => m.Language)
          .HasColumnName("Language")
          .HasConversion<string>()
          .HasMaxLength(20)
          .IsRequired();

      // Unique constraint on name and version within the metadata configuration
      metaBuilder.HasIndex(m => new { m.Name, m.Version })
          .IsUnique()
          .HasDatabaseName("IX_Plugins_Name_Version");
    });

    // Configure properties
    builder.Property(p => p.EntryPoint)
        .HasMaxLength(500)
        .IsRequired();

    builder.Property(p => p.PluginPath)
        .HasMaxLength(1000)
        .IsRequired();

    // Configure Capabilities as JSON array
    builder.Property(p => p.Capabilities)
        .HasConversion(
            v => JsonSerializer.Serialize(v, JsonValueComparerHelper._jsonOptions),
            v => (IReadOnlyList<string>)(JsonSerializer.Deserialize<List<string>>(v, JsonValueComparerHelper._jsonOptions) ?? new List<string>()))
        .HasColumnType("TEXT")
        .Metadata.SetValueComparer(
            new ValueComparer<IReadOnlyList<string>>(
                (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2, StringComparer.Ordinal),
                c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, StringComparer.Ordinal.GetHashCode(v ?? string.Empty))),
                c => c == null ? new List<string>().AsReadOnly() : c.ToList().AsReadOnly()));

    // Configure Configuration as JSON
    builder.Property(p => p.Configuration)
        .HasConversion(
            v => JsonSerializer.Serialize(v, JsonValueComparerHelper._jsonOptions),
            v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, JsonValueComparerHelper._jsonOptions) ?? new Dictionary<string, object>())
        .HasColumnType("TEXT")
        .Metadata.SetValueComparer(
            JsonValueComparerHelper.CreateJsonValueComparer<Dictionary<string, object>>());

    builder.Property(p => p.Status)
        .HasConversion<string>()
        .HasMaxLength(20)
        .IsRequired();

    builder.Property(p => p.RegisteredAt)
        .IsRequired();

    builder.Property(p => p.LastValidatedAt);

    builder.Property(p => p.LastExecutedAt);

    builder.Property(p => p.ExecutionCount)
        .IsRequired();

    builder.Property(p => p.ErrorMessage)
        .HasMaxLength(2000);

    // Indexes
    builder.HasIndex(p => p.Status)
        .HasDatabaseName("IX_Plugins_Status");

    // Ignore domain events (not persisted)
    builder.Ignore(p => p.DomainEvents);
  }
}