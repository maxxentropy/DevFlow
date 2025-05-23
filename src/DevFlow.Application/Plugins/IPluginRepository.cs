using DevFlow.Application.Common;
using DevFlow.Domain.Common;
using DevFlow.Domain.Plugins.Entities;
using DevFlow.Domain.Plugins.Enums;

namespace DevFlow.Application.Plugins;

/// <summary>
/// Repository interface for plugin operations.
/// </summary>
public interface IPluginRepository : IRepository<Plugin, PluginId>
{
    /// <summary>
    /// Gets all available plugins.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A list of all plugins</returns>
    Task<IReadOnlyList<Plugin>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets plugins by language.
    /// </summary>
    /// <param name="language">The plugin language</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A list of plugins for the specified language</returns>
    Task<IReadOnlyList<Plugin>> GetByLanguageAsync(PluginLanguage language, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a plugin exists with the specified name and version.
    /// </summary>
    /// <param name="name">The plugin name</param>
    /// <param name="version">The plugin version</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>True if the plugin exists, otherwise false</returns>
    Task<bool> ExistsAsync(string name, string version, CancellationToken cancellationToken = default);
}