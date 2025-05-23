using DevFlow.Application.Common;
using DevFlow.Domain.Common;

namespace DevFlow.Application.Workflows.Commands;

/// <summary>
/// Command to create a new workflow.
/// </summary>
/// <param name="Name">The workflow name</param>
/// <param name="Description">The workflow description</param>
public sealed record CreateWorkflowCommand(
    string Name,
    string Description) : ICommand<WorkflowId>;