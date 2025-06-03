using DevFlow.Application.Common;
using DevFlow.Application.Workflows;
using DevFlow.Application.Workflows.Commands;
using DevFlow.Application.Plugins;
using DevFlow.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace DevFlow.Application.Workflows.Commands.Handlers;

/// <summary>
/// Handler for adding steps to workflows.
/// </summary>
public sealed class AddWorkflowStepCommandHandler : ICommandHandler<AddWorkflowStepCommand>
{
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IPluginRepository _pluginRepository;
    private readonly ILogger<AddWorkflowStepCommandHandler> _logger;

    public AddWorkflowStepCommandHandler(
        IWorkflowRepository workflowRepository,
        IPluginRepository pluginRepository,
        ILogger<AddWorkflowStepCommandHandler> logger)
    {
        _workflowRepository = workflowRepository;
        _pluginRepository = pluginRepository;
        _logger = logger;
    }

    public async Task<Result> Handle(AddWorkflowStepCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Adding step '{StepName}' to workflow {WorkflowId}", 
            request.StepName, request.WorkflowId.Value);

        // Get the workflow
        var workflow = await _workflowRepository.GetByIdAsync(request.WorkflowId, cancellationToken);
        if (workflow is null)
        {
            var error = Error.NotFound("Workflow.NotFound", $"Workflow with ID '{request.WorkflowId.Value}' was not found.");
            _logger.LogWarning("Failed to add step: {Error}", error.Message);
            return Result.Failure(error);
        }

        // Verify plugin exists
        var plugin = await _pluginRepository.GetByIdAsync(request.PluginId, cancellationToken);
        if (plugin is null)
        {
            var error = Error.NotFound("Plugin.NotFound", $"Plugin with ID '{request.PluginId.Value}' was not found.");
            _logger.LogWarning("Failed to add step: {Error}", error.Message);
            return Result.Failure(error);
        }

        // Add the step to the workflow
        var addStepResult = workflow.AddStep(request.StepName, request.PluginId, request.Configuration, request.Order);
        if (addStepResult.IsFailure)
        {
            _logger.LogWarning("Failed to add step to workflow {WorkflowId}: {Error}", 
                request.WorkflowId.Value, addStepResult.Error.Message);
            return addStepResult;
        }

        try
        {
            // Save changes
            await _workflowRepository.UpdateAsync(workflow, cancellationToken);
            await _workflowRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully added step '{StepName}' to workflow {WorkflowId}", 
                request.StepName, request.WorkflowId.Value);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save workflow {WorkflowId} after adding step", request.WorkflowId.Value);
            return Result.Failure(Error.Failure("Workflow.SaveFailed", "Failed to save workflow changes."));
        }
    }
}