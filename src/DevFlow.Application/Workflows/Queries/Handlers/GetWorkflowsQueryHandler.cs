using DevFlow.Application.Common;
using DevFlow.Application.Workflows;
using DevFlow.Application.Workflows.DTOs;
using DevFlow.Application.Workflows.Queries;
using DevFlow.Domain.Workflows.Enums;
using DevFlow.SharedKernel.Common;
using Microsoft.Extensions.Logging;

namespace DevFlow.Application.Workflows.Queries.Handlers;

/// <summary>
/// Handler for getting a paginated list of workflows.
/// </summary>
public sealed class GetWorkflowsQueryHandler : IQueryHandler<GetWorkflowQuery, PagedResult<WorkflowDto>>
{
    private readonly IWorkflowRepository _workflowRepository;
    private readonly ILogger<GetWorkflowsQueryHandler> _logger;

    public GetWorkflowsQueryHandler(
        IWorkflowRepository workflowRepository,
        ILogger<GetWorkflowsQueryHandler> logger)
    {
        _workflowRepository = workflowRepository;
        _logger = logger;
    }

    public async Task<Result<PagedResult<WorkflowDto>>> Handle(GetWorkflowQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting workflows: Page {PageNumber}, Size {PageSize}, Status {Status}, Search '{SearchTerm}'",
            request.PageNumber, request.PageSize, request.Status, request.SearchTerm);

        try
        {
            // Parse status filter if provided
            WorkflowStatus? statusFilter = null;
            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                if (Enum.TryParse<WorkflowStatus>(request.Status, true, out var status))
                {
                    statusFilter = status;
                }
                else
                {
                    var error = Error.Validation("GetWorkflows.InvalidStatus", $"Invalid status value: '{request.Status}'.");
                    _logger.LogWarning("Invalid status filter: {Status}", request.Status);
                    return Result<PagedResult<WorkflowDto>>.Failure(error);
                }
            }

            var result = await _workflowRepository.GetWorkflowsAsync(
                request.PageNumber,
                request.PageSize,
                statusFilter,
                request.SearchTerm,
                cancellationToken);

            _logger.LogInformation("Successfully retrieved {Count} workflows (Total: {Total})", 
                result.Items.Count, result.TotalCount);

            return Result<PagedResult<WorkflowDto>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve workflows");
            return Result<PagedResult<WorkflowDto>>.Failure(Error.Failure("Workflows.RetrieveFailed", "Failed to retrieve workflows."));
        }
    }
}