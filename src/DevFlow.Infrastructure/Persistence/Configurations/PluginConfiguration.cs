using DevFlow.Domain.Common;
using DevFlow.Domain.Plugins.Entities;
using DevFlow.Domain.Plugins.Enums;
using DevFlow.Domain.Plugins.ValueObjects;
using DevFlow.SharedKernel.Results;
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

    // Configure Dependencies as JSON with proper serialization
    builder.Property(p => p.Dependencies)
        .HasConversion(
            v => DependencyConverter.SerializeDependencies(v),
            v => DependencyConverter.DeserializeDependencies(v))
        .HasColumnType("TEXT")
        .HasDefaultValueSql("'[]'")
        .Metadata.SetValueComparer(
            new ValueComparer<IReadOnlyList<PluginDependency>>(
                (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
                c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c == null ? new List<PluginDependency>().AsReadOnly() : c.ToList().AsReadOnly()));
        

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

    builder.Property(p => p.SourceHash)
    .HasMaxLength(64); // SHA256 hash is 64 characters

    // Indexes
    builder.HasIndex(p => p.Status)
        .HasDatabaseName("IX_Plugins_Status");

    builder.HasIndex(p => p.SourceHash)
        .HasDatabaseName("IX_Plugins_SourceHash");

    // Ignore domain events (not persisted)
    builder.Ignore(p => p.DomainEvents);
  }
}

/// <summary>
/// DTO for serializing plugin dependencies to JSON.
/// </summary>
internal record DependencyDto
{
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string? Source { get; init; }
}

/// <summary>
/// Static converter methods for serializing/deserializing plugin dependencies.
/// </summary>
internal static class DependencyConverter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string SerializeDependencies(IReadOnlyList<PluginDependency> dependencies)
    {
        if (dependencies == null || !dependencies.Any())
            return "[]";

        var dtos = dependencies.Select(d => new DependencyDto
        {
            Name = d.Name,
            Version = d.Version,
            Type = d.Type.ToString(),
            Source = d.Source
        }).ToList();

        return JsonSerializer.Serialize(dtos, JsonOptions);
    }

    public static IReadOnlyList<PluginDependency> DeserializeDependencies(string json)
    {
        var list = DeserializeToList(json);
        return list; // Return the list directly, as List<T> implements IReadOnlyList<T>
    }

    public static string SerializeList(List<PluginDependency> dependencies)
    {
        if (dependencies == null || !dependencies.Any())
            return "[]";

        var dtos = dependencies.Select(d => new DependencyDto
        {
            Name = d.Name,
            Version = d.Version,
            Type = d.Type.ToString(),
            Source = d.Source
        }).ToList();

        return JsonSerializer.Serialize(dtos, JsonOptions);
    }

    public static List<PluginDependency> DeserializeToList(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
            return new List<PluginDependency>();

        try
        {
            var dtos = JsonSerializer.Deserialize<List<DependencyDto>>(json, JsonOptions);
            if (dtos == null)
                return new List<PluginDependency>();

            var dependencies = new List<PluginDependency>();
            foreach (var dto in dtos)
            {
                if (!Enum.TryParse<PluginDependencyType>(dto.Type, out var type))
                    continue;

                var dependencyResult = type switch
                {
                    PluginDependencyType.NuGetPackage => PluginDependency.CreateNuGetPackage(dto.Name, dto.Version, dto.Source),
                    PluginDependencyType.Plugin => PluginDependency.CreatePluginDependency(dto.Name, dto.Version),
                    PluginDependencyType.FileReference => PluginDependency.CreateFileReference(dto.Name, dto.Version, dto.Source ?? dto.Name),
                    _ => (Result<PluginDependency>?)null
                };

                if (dependencyResult?.IsSuccess == true)
                {
                    dependencies.Add(dependencyResult.Value);
                }
            }

            return dependencies;
        }
        catch
        {
            return new List<PluginDependency>();
        }
    }
}
