namespace DevFlow.SharedKernel.ValueObjects;

/// <summary>
/// Base class for value objects.
/// </summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
  /// <summary>
  /// Gets the atomic values that define the equality of this value object.
  /// </summary>
  /// <returns>An enumerable of objects representing the equality components</returns>
  protected abstract IEnumerable<object?> GetEqualityComponents();

  /// <summary>
  /// Determines whether two value objects are equal.
  /// </summary>
  public bool Equals(ValueObject? other)
  {
    if (other is null) return false;
    if (ReferenceEquals(this, other)) return true;
    if (GetType() != other.GetType()) return false;

    return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
  }

  /// <summary>
  /// Determines whether the specified object is equal to the current value object.
  /// </summary>
  public override bool Equals(object? obj)
  {
    return obj is ValueObject valueObject && Equals(valueObject);
  }

  /// <summary>
  /// Returns the hash code for this value object.
  /// </summary>
  public override int GetHashCode()
  {
    return GetEqualityComponents()
        .Aggregate(1, (current, obj) =>
        {
          unchecked
          {
            return current * 23 + (obj?.GetHashCode() ?? 0);
          }
        });
  }

  /// <summary>
  /// Equality operator.
  /// </summary>
  public static bool operator ==(ValueObject? left, ValueObject? right)
  {
    return left?.Equals(right) ?? right is null;
  }

  /// <summary>
  /// Inequality operator.
  /// </summary>
  public static bool operator !=(ValueObject? left, ValueObject? right)
  {
    return !(left == right);
  }

  /// <summary>
  /// Returns a string representation of this value object.
  /// </summary>
  public override string ToString()
  {
    return $"{GetType().Name} [{string.Join(", ", GetEqualityComponents())}]";
  }
}