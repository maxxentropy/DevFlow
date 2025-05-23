namespace DevFlow.SharedKernel.Common;

/// <summary>
/// Base class for value objects in the domain model.
/// Value objects are immutable and compared by structural equality.
/// </summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
    /// <summary>
    /// Gets the atomic values that make up this value object.
    /// Used for equality comparison.
    /// </summary>
    /// <returns>The atomic values of this value object</returns>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public bool Equals(ValueObject? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;
        
        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override bool Equals(object? obj)
    {
        return obj is ValueObject valueObject && Equals(valueObject);
    }

    public override int GetHashCode()
    {
        return GetEqualityComponents()
            .Where(x => x is not null)
            .Aggregate(1, (current, obj) => current * 23 + obj!.GetHashCode());
    }

    public static bool operator ==(ValueObject? left, ValueObject? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(ValueObject? left, ValueObject? right)
    {
        return !Equals(left, right);
    }
}