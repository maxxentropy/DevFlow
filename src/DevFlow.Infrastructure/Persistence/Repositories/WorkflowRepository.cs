using DevFlow.Application.Common;
using DevFlow.Application.Workflows;
using DevFlow.Application.Workflows.DTOs;
using DevFlow.Domain.Common;
using DevFlow.Domain.Workflows.Entities;
using DevFlow.Domain.Workflows.Enums;
using DevFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DevFlow.SharedKernel.Entities;

namespace DevFlow.Infrastructure.Persistence.Repositories;

/// <summary>
/// Entity Framework implementation of the workflow repository.
/// </summary>
public sealed class WorkflowRepository : IWorkflowRepository
{
  private readonly DevFlowDbContext _context;
  private readonly ILogger<WorkflowRepository> _logger;

  public WorkflowRepository(DevFlowDbContext context, ILogger<WorkflowRepository> logger)
  {
    _context = context;
    _logger = logger;
  }

  public async Task<Workflow?> GetByIdAsync(WorkflowId id, CancellationToken cancellationToken = default)
  {
    return await _context.Workflows
        .Include(w => w.Steps.OrderBy(s => s.Order))
        .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
  }

  public async Task AddAsync(Workflow entity, CancellationToken cancellationToken = default)
  {
    await _context.Workflows.AddAsync(entity, cancellationToken);
  }

  public Task UpdateAsync(Workflow entity, CancellationToken cancellationToken = default)
  {
    _context.Workflows.Update(entity);
    return Task.CompletedTask;
  }

  public Task RemoveAsync(Workflow entity, CancellationToken cancellationToken = default)
  {
    _context.Workflows.Remove(entity);
    return Task.CompletedTask;
  }

  public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
  {
    return await _context.SaveChangesAsync(cancellationToken);
  }

  public async Task<PagedResult<WorkflowDto>> GetWorkflowsAsync(
      int pageNumber,
      int pageSize,
      WorkflowStatus? status = null,
      string? searchTerm = null,
      CancellationToken cancellationToken = default)
  {
    var query = _context.Workflows
        .Include(w => w.Steps.OrderBy(s => s.Order))
        .AsQueryable();

    // Apply status filter
    if (status.HasValue)
    {
      query = query.Where(w => w.Status == status.Value);
    }

    // Apply search filter
    if (!string.IsNullOrWhiteSpace(searchTerm))
    {
      var searchLower = searchTerm.ToLower();
      query = query.Where(w =>
          w.Name.Value.ToLower().Contains(searchLower) ||
          w.Description.Value.ToLower().Contains(searchLower));
    }

    // Get total count
    var totalCount = await query.CountAsync(cancellationToken);

    // Apply pagination and ordering
    var workflows = await query
        .OrderByDescending(w => w.CreatedAt)
        .Skip((pageNumber - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync(cancellationToken);

    // Map to DTOs
    var workflowDtos = workflows.Select(MapToDto).ToList();

    return new PagedResult<WorkflowDto>
    {
      Items = workflowDtos,
      TotalCount = totalCount,
      PageNumber = pageNumber,
      PageSize = pageSize
    };
  }

  public async Task<WorkflowDto?> GetWorkflowDtoAsync(WorkflowId id, CancellationToken cancellationToken = default)
  {
    var workflow = await GetByIdAsync(id, cancellationToken);
    return workflow is not null ? MapToDto(workflow) : null;
  }

  public async Task<bool> ExistsWithNameAsync(string name, WorkflowId? excludeId = null, CancellationToken cancellationToken = default)
  {
    var query = _context.Workflows.Where(w => w.Name.Value == name);

    if (excludeId is not null)
    {
      query = query.Where(w => w.Id != excludeId);
    }

    return await query.AnyAsync(cancellationToken);
  }

  private static WorkflowDto MapToDto(Workflow workflow)
  {
    return new WorkflowDto
    {
      Id = workflow.Id.Value,
      Name = workflow.Name.Value,
      Description = workflow.Description.Value,
      Status = workflow.Status,
      CreatedAt = workflow.CreatedAt,
      UpdatedAt = workflow.UpdatedAt,
      StartedAt = workflow.StartedAt,
      CompletedAt = workflow.CompletedAt,
      ErrorMessage = workflow.ErrorMessage,
      Steps = workflow.Steps.Select(MapStepToDto).ToList()
    };
  }

  private static WorkflowStepDto MapStepToDto(WorkflowStep step)
  {
    return new WorkflowStepDto
    {
      Id = step.Id.Value,
      Name = step.Name,
      PluginId = step.PluginId.Value,
      Order = step.Order,
      Configuration = step.Configuration,
      Status = step.Status,
      CreatedAt = step.CreatedAt,
      StartedAt = step.StartedAt,
      CompletedAt = step.CompletedAt,
      ErrorMessage = step.ErrorMessage,
      Output = step.Output,
      ExecutionDurationMs = step.ExecutionDurationMs
    };
  }
}