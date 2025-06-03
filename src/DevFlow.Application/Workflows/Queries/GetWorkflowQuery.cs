using DevFlow.Application.Common;
using DevFlow.Application.Workflows.DTOs;
using DevFlow.Domain.Common;

namespace DevFlow.Application.Workflows.Queries;



/// <summary>
/// Query to get a workflow by its identifier.
/// </summary>
/// <param name="WorkflowId">The workflow identifier</param>
public sealed record GetWorkflowQuery(WorkflowId WorkflowId) : IQuery<WorkflowDto>;