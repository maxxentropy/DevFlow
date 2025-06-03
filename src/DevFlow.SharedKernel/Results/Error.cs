namespace DevFlow.SharedKernel.Results;

/// <summary>
/// Represents an error that can occur in the application.
/// </summary>
public sealed record Error
{
  /// <summary>
  /// Gets the error code.
  /// </summary>
  public string Code { get; }

  /// <summary>
  /// Gets the error message.
  /// </summary>
  public string Message { get; }

  /// <summary>
  /// Gets the error type.
  /// </summary>
  public ErrorType Type { get; }

  /// <summary>
  /// Gets additional error metadata.
  /// </summary>
  public Dictionary<string, object>? Metadata { get; }

  /// <summary>
  /// Initializes a new instance of the <see cref="Error"/> class.
  /// </summary>
  private Error(string code, string message, ErrorType type, Dictionary<string, object>? metadata = null)
  {
    Code = code;
    Message = message;
    Type = type;
    Metadata = metadata;
  }

  /// <summary>
  /// Creates a validation error.
  /// </summary>
  public static Error Validation(string code, string message, Dictionary<string, object>? metadata = null) =>
      new(code, message, ErrorType.Validation, metadata);

  /// <summary>
  /// Creates a not found error.
  /// </summary>
  public static Error NotFound(string code, string message, Dictionary<string, object>? metadata = null) =>
      new(code, message, ErrorType.NotFound, metadata);

  /// <summary>
  /// Creates a conflict error.
  /// </summary>
  public static Error Conflict(string code, string message, Dictionary<string, object>? metadata = null) =>
      new(code, message, ErrorType.Conflict, metadata);

  /// <summary>
  /// Creates a failure error.
  /// </summary>
  public static Error Failure(string code, string message, Dictionary<string, object>? metadata = null) =>
      new(code, message, ErrorType.Failure, metadata);

  /// <summary>
  /// Creates an unexpected error.
  /// </summary>
  public static Error Unexpected(string code, string message, Dictionary<string, object>? metadata = null) =>
      new(code, message, ErrorType.Unexpected, metadata);

  /// <summary>
  /// Creates an unauthorized error.
  /// </summary>
  public static Error Unauthorized(string code, string message, Dictionary<string, object>? metadata = null) =>
      new(code, message, ErrorType.Unauthorized, metadata);

  /// <summary>
  /// Creates a forbidden error.
  /// </summary>
  public static Error Forbidden(string code, string message, Dictionary<string, object>? metadata = null) =>
      new(code, message, ErrorType.Forbidden, metadata);

  /// <summary>
  /// Represents a null/empty error.
  /// </summary>
  public static readonly Error None = new(string.Empty, string.Empty, ErrorType.None);

  /// <summary>
  /// Represents a general failure error.
  /// </summary>
  public static readonly Error General = new("General.Failure", "A general failure occurred.", ErrorType.Failure);

  public override string ToString() => $"{Type}: {Code} - {Message}";
}