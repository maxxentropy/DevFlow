namespace DevFlow.SharedKernel.Results;

/// <summary>
/// Represents the type of error.
/// </summary>
public enum ErrorType
{
  /// <summary>
  /// No error.
  /// </summary>
  None = 0,

  /// <summary>
  /// A validation error.
  /// </summary>
  Validation = 1,

  /// <summary>
  /// A not found error.
  /// </summary>
  NotFound = 2,

  /// <summary>
  /// A conflict error.
  /// </summary>
  Conflict = 3,

  /// <summary>
  /// A general failure error.
  /// </summary>
  Failure = 4,

  /// <summary>
  /// An unexpected error.
  /// </summary>
  Unexpected = 5,

  /// <summary>
  /// An unauthorized error.
  /// </summary>
  Unauthorized = 6,

  /// <summary>
  /// A forbidden error.
  /// </summary>
  Forbidden = 7
}