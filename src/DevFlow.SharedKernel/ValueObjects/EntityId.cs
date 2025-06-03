using System.ComponentModel;
using System.Text.Json.Serialization;

namespace DevFlow.SharedKernel.ValueObjects;

/// <summary>
/// Base class for entity identifiers.
/// </summary>
public abstract class EntityId : StronglyTypedId
{
  /// <summary>
  /// Initializes a new instance of the entity identifier.
  /// </summary>
  /// <param name="value">The underlying Guid value</param>
  protected EntityId(Guid value) : base(value)
  {
  }
}

/// <summary>
/// Generic entity identifier.
/// </summary>
/// <typeparam name="T">The entity type this identifier belongs to</typeparam>
public sealed class EntityId<T> : EntityId
{
  /// <summary>
  /// Initializes a new instance of the entity identifier.
  /// </summary>
  /// <param name="value">The underlying Guid value</param>
  public EntityId(Guid value) : base(value)
  {
  }

  /// <summary>
  /// Creates a new entity identifier with a new Guid value.
  /// </summary>
  /// <returns>A new entity identifier</returns>
  public static EntityId<T> New() => new(Guid.NewGuid());

  /// <summary>
  /// Creates an entity identifier from the specified Guid value.
  /// </summary>
  /// <param name="value">The Guid value</param>
  /// <returns>An entity identifier</returns>
  public static EntityId<T> From(Guid value) => new(value);

  /// <summary>
  /// Creates an entity identifier from the specified string value.
  /// </summary>
  /// <param name="value">The string value</param>
  /// <returns>An entity identifier</returns>
  public static EntityId<T> From(string value)
  {
    if (string.IsNullOrWhiteSpace(value))
      throw new ArgumentException("Entity identifier string cannot be null or empty", nameof(value));

    if (!Guid.TryParse(value, out var guid))
      throw new ArgumentException("Invalid Guid format", nameof(value));

    return new EntityId<T>(guid);
  }

  /// <summary>
  /// Implicit conversion from EntityId to Guid.
  /// </summary>
  public static implicit operator Guid(EntityId<T> id) => id.Value;

  /// <summary>
  /// Explicit conversion from Guid to EntityId.
  /// </summary>
  public static explicit operator EntityId<T>(Guid value) => new(value);

  /// <summary>
  /// Explicit conversion from string to EntityId.
  /// </summary>
  public static explicit operator EntityId<T>(string value) => From(value);
}