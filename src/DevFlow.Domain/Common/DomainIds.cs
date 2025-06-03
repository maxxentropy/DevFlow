using DevFlow.SharedKernel.ValueObjects;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace DevFlow.Domain.Common;

/// <summary>
/// Strongly-typed identifier for Workflow entities.
/// </summary>
[TypeConverter(typeof(WorkflowIdConverter))]
[JsonConverter(typeof(WorkflowIdJsonConverter))]
public sealed class WorkflowId : EntityId, IEntityId
{
  public WorkflowId(Guid value) : base(value) { }

  /// <summary>
  /// Gets the value as object for IEntityId interface.
  /// </summary>
  object IEntityId.Value => Value;

  /// <summary>
  /// Gets the string representation of the identifier.
  /// </summary>
  public string StringValue => Value.ToString();

  /// <summary>
  /// Determines whether the specified IEntityId is equal to the current WorkflowId.
  /// </summary>
  public bool Equals(IEntityId? other)
  {
    return other is WorkflowId otherId && Value.Equals(otherId.Value);
  }

  /// <summary>
  /// Creates a new unique workflow identifier.
  /// </summary>
  public static WorkflowId New() => new(Guid.NewGuid());

  /// <summary>
  /// Creates a workflow identifier from a Guid value.
  /// </summary>
  public static WorkflowId From(Guid value) => new(value);

  /// <summary>
  /// Creates a workflow identifier from a string value.
  /// </summary>
  public static WorkflowId From(string value)
  {
    if (string.IsNullOrWhiteSpace(value))
      throw new ArgumentException("Workflow identifier string cannot be null or empty", nameof(value));

    if (!Guid.TryParse(value, out var guid))
      throw new ArgumentException("Invalid Guid format", nameof(value));

    return new WorkflowId(guid);
  }

  /// <summary>
  /// Implicit conversion from WorkflowId to Guid.
  /// </summary>
  public static implicit operator Guid(WorkflowId id) => id.Value;

  /// <summary>
  /// Explicit conversion from Guid to WorkflowId.
  /// </summary>
  public static explicit operator WorkflowId(Guid value) => new(value);

  /// <summary>
  /// Explicit conversion from string to WorkflowId.
  /// </summary>
  public static explicit operator WorkflowId(string value) => From(value);
}

/// <summary>
/// Strongly-typed identifier for Plugin entities.
/// </summary>
[TypeConverter(typeof(PluginIdConverter))]
[JsonConverter(typeof(PluginIdJsonConverter))]
public sealed class PluginId : EntityId, IEntityId
{
  public PluginId(Guid value) : base(value) { }

  /// <summary>
  /// Gets the value as object for IEntityId interface.
  /// </summary>
  object IEntityId.Value => Value;

  /// <summary>
  /// Gets the string representation of the identifier.
  /// </summary>
  public string StringValue => Value.ToString();

  /// <summary>
  /// Determines whether the specified IEntityId is equal to the current PluginId.
  /// </summary>
  public bool Equals(IEntityId? other)
  {
    return other is PluginId otherId && Value.Equals(otherId.Value);
  }

  /// <summary>
  /// Creates a new unique plugin identifier.
  /// </summary>
  public static PluginId New() => new(Guid.NewGuid());

  /// <summary>
  /// Creates a plugin identifier from a Guid value.
  /// </summary>
  public static PluginId From(Guid value) => new(value);

  /// <summary>
  /// Creates a plugin identifier from a string value.
  /// </summary>
  public static PluginId From(string value)
  {
    if (string.IsNullOrWhiteSpace(value))
      throw new ArgumentException("Plugin identifier string cannot be null or empty", nameof(value));

    if (!Guid.TryParse(value, out var guid))
      throw new ArgumentException("Invalid Guid format", nameof(value));

    return new PluginId(guid);
  }

  /// <summary>
  /// Implicit conversion from PluginId to Guid.
  /// </summary>
  public static implicit operator Guid(PluginId id) => id.Value;

  /// <summary>
  /// Explicit conversion from Guid to PluginId.
  /// </summary>
  public static explicit operator PluginId(Guid value) => new(value);

  /// <summary>
  /// Explicit conversion from string to PluginId.
  /// </summary>
  public static explicit operator PluginId(string value) => From(value);
}

/// <summary>
/// Strongly-typed identifier for WorkflowStep entities.
/// </summary>
[TypeConverter(typeof(WorkflowStepIdConverter))]
[JsonConverter(typeof(WorkflowStepIdJsonConverter))]
public sealed class WorkflowStepId : EntityId, IEntityId
{
  public WorkflowStepId(Guid value) : base(value) { }

  /// <summary>
  /// Gets the value as object for IEntityId interface.
  /// </summary>
  object IEntityId.Value => Value;

  /// <summary>
  /// Gets the string representation of the identifier.
  /// </summary>
  public string StringValue => Value.ToString();

  /// <summary>
  /// Determines whether the specified IEntityId is equal to the current WorkflowStepId.
  /// </summary>
  public bool Equals(IEntityId? other)
  {
    return other is WorkflowStepId otherId && Value.Equals(otherId.Value);
  }

  /// <summary>
  /// Creates a new unique workflow step identifier.
  /// </summary>
  public static WorkflowStepId New() => new(Guid.NewGuid());

  /// <summary>
  /// Creates a workflow step identifier from a Guid value.
  /// </summary>
  public static WorkflowStepId From(Guid value) => new(value);

  /// <summary>
  /// Creates a workflow step identifier from a string value.
  /// </summary>
  public static WorkflowStepId From(string value)
  {
    if (string.IsNullOrWhiteSpace(value))
      throw new ArgumentException("Workflow step identifier string cannot be null or empty", nameof(value));

    if (!Guid.TryParse(value, out var guid))
      throw new ArgumentException("Invalid Guid format", nameof(value));

    return new WorkflowStepId(guid);
  }

  /// <summary>
  /// Implicit conversion from WorkflowStepId to Guid.
  /// </summary>
  public static implicit operator Guid(WorkflowStepId id) => id.Value;

  /// <summary>
  /// Explicit conversion from Guid to WorkflowStepId.
  /// </summary>
  public static explicit operator WorkflowStepId(Guid value) => new(value);

  /// <summary>
  /// Explicit conversion from string to WorkflowStepId.
  /// </summary>
  public static explicit operator WorkflowStepId(string value) => From(value);
}

// Type converters for each ID type
public class WorkflowIdConverter : StronglyTypedIdConverter<WorkflowId> { }
public class WorkflowIdJsonConverter : StronglyTypedIdJsonConverter<WorkflowId> { }

public class PluginIdConverter : StronglyTypedIdConverter<PluginId> { }
public class PluginIdJsonConverter : StronglyTypedIdJsonConverter<PluginId> { }

public class WorkflowStepIdConverter : StronglyTypedIdConverter<WorkflowStepId> { }
public class WorkflowStepIdJsonConverter : StronglyTypedIdJsonConverter<WorkflowStepId> { }