using DevFlow.Application.Common;
using DevFlow.Domain.Common;

namespace DevFlow.Application.Workflows.Commands;

/// <summary>
/// Command to add a step to a workflow.
/// </summary>
/// <param name="WorkflowId">The workflow identifier</param>
/// <param name="StepName">The step name</param>
/// <param name="PluginId">The plugin identifier</param>
/// <param name="Configuration">The step configuration</param>
/// <param name="Order">The execution order</param>
public sealed record AddWorkflowStepCommand(
    WorkflowId WorkflowId,
    string StepName,
    PluginId PluginId,
    Dictionary<string, object>? Configuration = null,
    int? Order = null) : ICommand;