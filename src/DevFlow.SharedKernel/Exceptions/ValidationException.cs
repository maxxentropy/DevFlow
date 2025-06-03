namespace DevFlow.SharedKernel.Exceptions;

/// <summary>
/// Exception that is thrown when a validation error occurs.
/// </summary>
public sealed class ValidationException : DomainException
{
  /// <summary>
  /// Gets the validation errors.
  /// </summary>
  public IReadOnlyList<ValidationError> ValidationErrors { get; }

  /// <summary>
  /// Initializes a new instance of the <see cref="ValidationException"/> class.
  /// </summary>
  public ValidationException(IEnumerable<ValidationError> validationErrors)
      : base("Validation.Failed", "One or more validation errors occurred.")
  {
    ValidationErrors = validationErrors.ToList().AsReadOnly();
  }

  /// <summary>
  /// Initializes a new instance of the <see cref="ValidationException"/> class.
  /// </summary>
  public ValidationException(string propertyName, string errorMessage)
      : this(new[] { new ValidationError(propertyName, errorMessage) })
  {
  }

  /// <summary>
  /// Initializes a new instance of the <see cref="ValidationException"/> class.
  /// </summary>
  public ValidationException(string errorCode, string message, IEnumerable<ValidationError> validationErrors)
      : base(errorCode, message)
  {
    ValidationErrors = validationErrors.ToList().AsReadOnly();
  }

  public override string ToString()
  {
    var baseString = base.ToString();
    if (ValidationErrors.Any())
    {
      var errorsString = string.Join("; ", ValidationErrors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"));
      baseString += $" | Validation Errors: {errorsString}";
    }
    return baseString;
  }
}

/// <summary>
/// Represents a validation error.
/// </summary>
/// <param name="PropertyName">The name of the property that failed validation</param>
/// <param name="ErrorMessage">The validation error message</param>
public record ValidationError(string PropertyName, string ErrorMessage);