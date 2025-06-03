using DevFlow.Application.Common;
using DevFlow.Application.Workflows.DTOs;

namespace DevFlow.Application.Workflows.Queries;

/// <summary>
/// Query to get a list of workflows with optional filtering.
/// </summary>
/// <param name="PageNumber">The page number (1-based)</param>
/// <param name="PageSize">The number of items per page</param>
/// <param name="Status">Optional status filter</param>
/// <param name="SearchTerm">Optional search term for name/description</param>
public sealed record GetWorkflowsQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? Status = null,
    string? SearchTerm = null) : IQuery<PagedResult<WorkflowDto>>;