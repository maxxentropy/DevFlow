using DevFlow.SharedKernel.Common;

namespace DevFlow.Domain.Workflows.ValueObjects;

/// <summary>
/// Represents a workflow description with validation rules.
/// </summary>
public sealed class WorkflowDescription : ValueObject
{
    private const int MaxLength = 1000;

    private WorkflowDescription(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Gets the workflow description value.
    /// </summary>
    public string Value { get; private set; }

    /// <summary>
    /// Creates a new workflow description with validation.
    /// </summary>
    /// <param name="value">The workflow description value</param>
    /// <returns>A result containing the workflow description or validation errors</returns>
    public static Result<WorkflowDescription> Create(string value)
    {
        // Description can be empty, but if provided, must not exceed max length
        var trimmedValue = value?.Trim() ?? string.Empty;

        if (trimmedValue.Length > MaxLength)
            return Result<WorkflowDescription>.Failure(Error.Validation(
                "WorkflowDescription.TooLong", 
                $"Workflow description cannot exceed {MaxLength} characters."));

        return Result<WorkflowDescription>.Success(new WorkflowDescription(trimmedValue));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(WorkflowDescription description) => description.Value;
}