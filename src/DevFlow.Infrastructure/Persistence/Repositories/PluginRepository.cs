using DevFlow.Application.Common;
using DevFlow.Application.Plugins;
using DevFlow.Domain.Common;
using DevFlow.Domain.Plugins.Entities;
using DevFlow.Domain.Plugins.Enums;
using DevFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DevFlow.Infrastructure.Persistence.Repositories;

/// <summary>
/// Entity Framework implementation of the plugin repository.
/// </summary>
public sealed class PluginRepository : IPluginRepository
{
  private readonly DevFlowDbContext _context;
  private readonly ILogger<PluginRepository> _logger;

  public PluginRepository(DevFlowDbContext context, ILogger<PluginRepository> logger)
  {
    _context = context;
    _logger = logger;
  }

  public async Task<Plugin?> GetByIdAsync(PluginId id, CancellationToken cancellationToken = default)
  {
    return await _context.Plugins
        .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
  }

  public async Task AddAsync(Plugin entity, CancellationToken cancellationToken = default)
  {
    await _context.Plugins.AddAsync(entity, cancellationToken);
  }

  public Task UpdateAsync(Plugin entity, CancellationToken cancellationToken = default)
  {
    _context.Plugins.Update(entity);
    return Task.CompletedTask;
  }

  public Task RemoveAsync(Plugin entity, CancellationToken cancellationToken = default)
  {
    _context.Plugins.Remove(entity);
    return Task.CompletedTask;
  }

  public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
  {
    return await _context.SaveChangesAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<Plugin>> GetAllAsync(CancellationToken cancellationToken = default)
  {
    var plugins = await _context.Plugins
        .OrderBy(p => p.Metadata.Name)
        .ToListAsync(cancellationToken);

    return plugins.AsReadOnly();
  }

  public async Task<IReadOnlyList<Plugin>> GetByLanguageAsync(PluginLanguage language, CancellationToken cancellationToken = default)
  {
    var plugins = await _context.Plugins
        .Where(p => p.Metadata.Language == language)
        .OrderBy(p => p.Metadata.Name)
        .ToListAsync(cancellationToken);

    return plugins.AsReadOnly();
  }

  public async Task<bool> ExistsAsync(string name, string version, CancellationToken cancellationToken = default)
  {
    // Load plugins into memory to avoid EF Core conversion issues
    var plugins = await _context.Plugins.ToListAsync(cancellationToken);
    return plugins.Any(p => p.Metadata.Name == name && p.Metadata.Version.ToString() == version);
  }
}