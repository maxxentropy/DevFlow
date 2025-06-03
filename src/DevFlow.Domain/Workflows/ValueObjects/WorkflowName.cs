using DevFlow.SharedKernel.ValueObjects;
using DevFlow.SharedKernel.Results;

namespace DevFlow.Domain.Workflows.ValueObjects;

/// <summary>
/// Represents a workflow name with validation rules.
/// </summary>
public sealed class WorkflowName : ValueObject
{
    private const int MaxLength = 100;
    private const int MinLength = 3;

    private WorkflowName(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Gets the workflow name value.
    /// </summary>
    public string Value { get; private set; }

    /// <summary>
    /// Creates a new workflow name with validation.
    /// </summary>
    /// <param name="value">The workflow name value</param>
    /// <returns>A result containing the workflow name or validation errors</returns>
    public static Result<WorkflowName> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result<WorkflowName>.Failure(Error.Validation(
                "WorkflowName.Empty", 
                "Workflow name cannot be empty or whitespace."));

        if (value.Length < MinLength)
            return Result<WorkflowName>.Failure(Error.Validation(
                "WorkflowName.TooShort", 
                $"Workflow name must be at least {MinLength} characters long."));

        if (value.Length > MaxLength)
            return Result<WorkflowName>.Failure(Error.Validation(
                "WorkflowName.TooLong", 
                $"Workflow name cannot exceed {MaxLength} characters."));

        var trimmedValue = value.Trim();
        return Result<WorkflowName>.Success(new WorkflowName(trimmedValue));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(WorkflowName workflowName) => workflowName.Value;
}