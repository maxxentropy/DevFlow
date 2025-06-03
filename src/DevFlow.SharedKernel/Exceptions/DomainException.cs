namespace DevFlow.SharedKernel.Exceptions;

/// <summary>
/// Base exception for all domain-related exceptions.
/// </summary>
public abstract class DomainException : Exception
{
  /// <summary>
  /// Gets the error code associated with this exception.
  /// </summary>
  public string ErrorCode { get; }

  /// <summary>
  /// Gets additional metadata associated with this exception.
  /// </summary>
  public Dictionary<string, object>? Metadata { get; }

  /// <summary>
  /// Initializes a new instance of the <see cref="DomainException"/> class.
  /// </summary>
  protected DomainException(string errorCode, string message, Dictionary<string, object>? metadata = null)
      : base(message)
  {
    ErrorCode = errorCode;
    Metadata = metadata;
  }

  /// <summary>
  /// Initializes a new instance of the <see cref="DomainException"/> class.
  /// </summary>
  protected DomainException(string errorCode, string message, Exception innerException, Dictionary<string, object>? metadata = null)
      : base(message, innerException)
  {
    ErrorCode = errorCode;
    Metadata = metadata;
  }

  public override string ToString()
  {
    var result = $"{GetType().Name}: {ErrorCode} - {Message}";

    if (Metadata?.Any() == true)
    {
      var metadataString = string.Join(", ", Metadata.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
      result += $" | Metadata: {metadataString}";
    }

    if (InnerException != null)
    {
      result += $" | Inner: {InnerException}";
    }

    return result;
  }
}