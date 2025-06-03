namespace DevFlow.SharedKernel.ValueObjects;

/// <summary>
/// Marker interface for entity identifiers.
/// Ensures type safety for entity IDs.
/// </summary>
public interface IEntityId : IEquatable<IEntityId>
{
  /// <summary>
  /// Gets the value of the identifier as an object.
  /// </summary>
  object Value { get; }

  /// <summary>
  /// Gets the string representation of the identifier.
  /// </summary>
  string StringValue { get; }
}

/// <summary>
/// Generic interface for strongly-typed entity identifiers.
/// </summary>
/// <typeparam name="T">The type of the identifier value</typeparam>
public interface IEntityId<out T> : IEntityId
{
  /// <summary>
  /// Gets the strongly-typed value of the identifier.
  /// </summary>
  new T Value { get; }
}