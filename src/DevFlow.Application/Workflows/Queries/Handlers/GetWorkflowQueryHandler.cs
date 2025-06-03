using DevFlow.Application.Common;
using DevFlow.Application.Workflows;
using DevFlow.Application.Workflows.DTOs;
using DevFlow.Application.Workflows.Queries;
using DevFlow.SharedKernel.Results;
using DevFlow.SharedKernel.ValueObjects;
using Microsoft.Extensions.Logging;

namespace DevFlow.Application.Workflows.Queries.Handlers;

/// <summary>
/// Handler for getting a single workflow by ID.
/// </summary>
public sealed class GetWorkflowQueryHandler : IQueryHandler<GetWorkflowQuery, WorkflowDto>
{
    private readonly IWorkflowRepository _workflowRepository;
    private readonly ILogger<GetWorkflowQueryHandler> _logger;

    public GetWorkflowQueryHandler(
        IWorkflowRepository workflowRepository,
        ILogger<GetWorkflowQueryHandler> logger)
    {
        _workflowRepository = workflowRepository;
        _logger = logger;
    }

    public async Task<Result<WorkflowDto>> Handle(GetWorkflowQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting workflow {WorkflowId}", request.WorkflowId.Value);

        try
        {
            var workflowDto = await _workflowRepository.GetWorkflowDtoAsync(request.WorkflowId, cancellationToken);
            
            if (workflowDto is null)
            {
                var error = Error.NotFound("Workflow.NotFound", $"Workflow with ID '{request.WorkflowId.Value}' was not found.");
                _logger.LogWarning("Workflow not found: {WorkflowId}", request.WorkflowId.Value);
                return Result<WorkflowDto>.Failure(error);
            }

            _logger.LogInformation("Successfully retrieved workflow {WorkflowId}", request.WorkflowId.Value);
            return Result<WorkflowDto>.Success(workflowDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve workflow {WorkflowId}", request.WorkflowId.Value);
            return Result<WorkflowDto>.Failure(Error.Failure("Workflow.RetrieveFailed", "Failed to retrieve workflow."));
        }
    }
}