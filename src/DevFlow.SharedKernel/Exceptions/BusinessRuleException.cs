namespace DevFlow.SharedKernel.Exceptions;

/// <summary>
/// Exception that is thrown when a business rule is violated.
/// </summary>
public sealed class BusinessRuleException : DomainException
{
  /// <summary>
  /// Gets the name of the business rule that was violated.
  /// </summary>
  public string RuleName { get; }

  /// <summary>
  /// Initializes a new instance of the <see cref="BusinessRuleException"/> class.
  /// </summary>
  public BusinessRuleException(string ruleName, string message, Dictionary<string, object>? metadata = null)
      : base($"BusinessRule.{ruleName}", message, metadata)
  {
    RuleName = ruleName;
  }

  /// <summary>
  /// Initializes a new instance of the <see cref="BusinessRuleException"/> class.
  /// </summary>
  public BusinessRuleException(string ruleName, string message, Exception innerException, Dictionary<string, object>? metadata = null)
      : base($"BusinessRule.{ruleName}", message, innerException, metadata)
  {
    RuleName = ruleName;
  }
}