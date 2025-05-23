namespace DevFlow.SharedKernel.Common;

/// <summary>
/// Marker interface for entity identifiers.
/// Ensures type safety for entity IDs.
/// </summary>
public interface IEntityId : IEquatable<IEntityId>
{
    /// <summary>
    /// Gets the string representation of the identifier.
    /// </summary>
    string Value { get; }
}