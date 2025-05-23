using DevFlow.Application.Common;
using DevFlow.Application.Workflows.DTOs;
using DevFlow.Domain.Common;
using DevFlow.Domain.Workflows.Entities;
using DevFlow.Domain.Workflows.Enums;

namespace DevFlow.Application.Workflows;

/// <summary>
/// Repository interface for workflow operations.
/// </summary>
public interface IWorkflowRepository : IRepository<Workflow, WorkflowId>
{
    /// <summary>
    /// Gets workflows with pagination and optional filtering.
    /// </summary>
    /// <param name="pageNumber">The page number (1-based)</param>
    /// <param name="pageSize">The number of items per page</param>
    /// <param name="status">Optional status filter</param>
    /// <param name="searchTerm">Optional search term</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A paginated result of workflow DTOs</returns>
    Task<PagedResult<WorkflowDto>> GetWorkflowsAsync(
        int pageNumber,
        int pageSize,
        WorkflowStatus? status = null,
        string? searchTerm = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a workflow as a DTO by its identifier.
    /// </summary>
    /// <param name="id">The workflow identifier</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The workflow DTO if found, otherwise null</returns>
    Task<WorkflowDto?> GetWorkflowDtoAsync(WorkflowId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a workflow exists with the specified name.
    /// </summary>
    /// <param name="name">The workflow name</param>
    /// <param name="excludeId">Optional workflow ID to exclude from the check</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>True if a workflow exists with the name, otherwise false</returns>
    Task<bool> ExistsWithNameAsync(string name, WorkflowId? excludeId = null, CancellationToken cancellationToken = default);
}