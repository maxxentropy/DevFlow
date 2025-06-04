using DevFlow.Domain.Plugins.Entities;
using DevFlow.Domain.Plugins.Enums;

namespace DevFlow.Application.Plugins.Runtime;

/// <summary>
/// Factory interface for creating and managing plugin runtime managers.
/// Provides access to runtime managers based on plugin requirements and language support.
/// </summary>
public interface IPluginRuntimeManagerFactory
{
  /// <summary>
  /// Gets all available runtime managers registered in the system.
  /// </summary>
  /// <returns>Collection of all registered runtime managers</returns>
  IEnumerable<IPluginRuntimeManager> GetAllRuntimeManagers();

  /// <summary>
  /// Gets a runtime manager that can execute the specified plugin.
  /// Returns the first compatible runtime manager found.
  /// </summary>
  /// <param name="plugin">The plugin to find a runtime manager for</param>
  /// <returns>A compatible runtime manager, or null if none found</returns>
  IPluginRuntimeManager? GetRuntimeManager(Plugin plugin);

  /// <summary>
  /// Gets all runtime managers that support the specified programming language.
  /// </summary>
  /// <param name="language">The programming language</param>
  /// <returns>Collection of runtime managers that support the language</returns>
  IEnumerable<IPluginRuntimeManager> GetRuntimeManagersForLanguage(PluginLanguage language);

  /// <summary>
  /// Checks if any runtime manager is available for the specified language.
  /// </summary>
  /// <param name="language">The programming language to check</param>
  /// <returns>True if at least one runtime manager supports the language</returns>
  bool IsLanguageSupported(PluginLanguage language);

  /// <summary>
  /// Gets the runtime manager with the specified runtime identifier.
  /// </summary>
  /// <param name="runtimeId">The unique runtime identifier</param>
  /// <returns>The runtime manager with the specified ID, or null if not found</returns>
  IPluginRuntimeManager? GetRuntimeManagerById(string runtimeId);
}