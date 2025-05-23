using DevFlow.Application.Common;
using DevFlow.Domain.Common;

namespace DevFlow.Application.Workflows.Commands;

/// <summary>
/// Command to start a workflow execution.
/// </summary>
/// <param name="WorkflowId">The workflow identifier</param>
public sealed record StartWorkflowCommand(WorkflowId WorkflowId) : ICommand;