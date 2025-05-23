using DevFlow.Application.Common;
using DevFlow.Application.Workflows;
using DevFlow.Application.Workflows.Commands;
using DevFlow.Domain.Common;
using DevFlow.Domain.Workflows.Entities;
using DevFlow.SharedKernel.Common;
using Microsoft.Extensions.Logging;

namespace DevFlow.Application.Workflows.Commands.Handlers;

/// <summary>
/// Handler for creating new workflows.
/// </summary>
public sealed class CreateWorkflowCommandHandler : ICommandHandler<CreateWorkflowCommand, WorkflowId>
{
    private readonly IWorkflowRepository _workflowRepository;
    private readonly ILogger<CreateWorkflowCommandHandler> _logger;

    public CreateWorkflowCommandHandler(
        IWorkflowRepository workflowRepository,
        ILogger<CreateWorkflowCommandHandler> logger)
    {
        _workflowRepository = workflowRepository;
        _logger = logger;
    }

    public async Task<Result<WorkflowId>> Handle(CreateWorkflowCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating workflow with name: {WorkflowName}", request.Name);

        // Check if workflow with same name already exists
        var existsWithName = await _workflowRepository.ExistsWithNameAsync(request.Name, cancellationToken: cancellationToken);
        if (existsWithName)
        {
            var error = Error.Conflict("Workflow.NameAlreadyExists", $"A workflow with the name '{request.Name}' already exists.");
            _logger.LogWarning("Failed to create workflow: {Error}", error.Message);
            return Result<WorkflowId>.Failure(error);
        }

        // Create the workflow
        var workflowResult = Workflow.Create(request.Name, request.Description);
        if (workflowResult.IsFailure)
        {
            _logger.LogWarning("Failed to create workflow: {Error}", workflowResult.Error.Message);
            return Result<WorkflowId>.Failure(workflowResult.Error);
        }

        var workflow = workflowResult.Value;

        try
        {
            // Save to repository
            await _workflowRepository.AddAsync(workflow, cancellationToken);
            await _workflowRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully created workflow {WorkflowId} with name: {WorkflowName}", 
                workflow.Id.Value, workflow.Name.Value);

            return Result<WorkflowId>.Success(workflow.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save workflow {WorkflowId} to repository", workflow.Id.Value);
            return Result<WorkflowId>.Failure(Error.Failure("Workflow.SaveFailed", "Failed to save workflow to repository."));
        }
    }
}