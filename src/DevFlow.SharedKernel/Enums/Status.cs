namespace DevFlow.SharedKernel.Enums;

/// <summary>
/// Common status enumeration for entities.
/// </summary>
public enum Status
{
  /// <summary>
  /// The entity is active.
  /// </summary>
  Active = 1,

  /// <summary>
  /// The entity is inactive.
  /// </summary>
  Inactive = 2,

  /// <summary>
  /// The entity is pending.
  /// </summary>
  Pending = 3,

  /// <summary>
  /// The entity is suspended.
  /// </summary>
  Suspended = 4,

  /// <summary>
  /// The entity is deleted.
  /// </summary>
  Deleted = 5
}