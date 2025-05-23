using DevFlow.Application.Common;
using DevFlow.Application.Workflows;
using DevFlow.Application.Workflows.Commands;
using DevFlow.SharedKernel.Common;
using Microsoft.Extensions.Logging;

namespace DevFlow.Application.Workflows.Commands.Handlers;

/// <summary>
/// Handler for starting workflow execution.
/// </summary>
public sealed class StartWorkflowCommandHandler : ICommandHandler<StartWorkflowCommand>
{
    private readonly IWorkflowRepository _workflowRepository;
    private readonly ILogger<StartWorkflowCommandHandler> _logger;

    public StartWorkflowCommandHandler(
        IWorkflowRepository workflowRepository,
        ILogger<StartWorkflowCommandHandler> logger)
    {
        _workflowRepository = workflowRepository;
        _logger = logger;
    }

    public async Task<Result> Handle(StartWorkflowCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting workflow {WorkflowId}", request.WorkflowId.Value);

        // Get the workflow
        var workflow = await _workflowRepository.GetByIdAsync(request.WorkflowId, cancellationToken);
        if (workflow is null)
        {
            var error = Error.NotFound("Workflow.NotFound", $"Workflow with ID '{request.WorkflowId.Value}' was not found.");
            _logger.LogWarning("Failed to start workflow: {Error}", error.Message);
            return Result.Failure(error);
        }

        // Start the workflow
        var startResult = workflow.Start();
        if (startResult.IsFailure)
        {
            _logger.LogWarning("Failed to start workflow {WorkflowId}: {Error}", 
                request.WorkflowId.Value, startResult.Error.Message);
            return startResult;
        }

        try
        {
            // Save changes
            await _workflowRepository.UpdateAsync(workflow, cancellationToken);
            await _workflowRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully started workflow {WorkflowId}", request.WorkflowId.Value);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save workflow {WorkflowId} after starting", request.WorkflowId.Value);
            return Result.Failure(Error.Failure("Workflow.SaveFailed", "Failed to save workflow changes."));
        }
    }
}