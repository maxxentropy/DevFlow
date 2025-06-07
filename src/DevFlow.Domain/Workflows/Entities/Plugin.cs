using DevFlow.Domain.Common;
using DevFlow.Domain.Plugins.ValueObjects;
using DevFlow.Domain.Plugins.Enums;
using DevFlow.Domain.Plugins.Events;
using DevFlow.SharedKernel.Entities;
using DevFlow.SharedKernel.Results;

namespace DevFlow.Domain.Plugins.Entities;

/// <summary>
/// Represents a plugin that can be executed within a workflow.
/// </summary>
public sealed class Plugin : AggregateRoot<PluginId>
{
  private readonly List<string> _capabilities = new();
  private readonly List<PluginDependency> _dependencies = new();

  // Private constructor for persistence
  private Plugin()
  {
    Id = default!; // Will be set by EF Core
  }

  // Constructor for creating new plugins
  private Plugin(
      PluginId id,
      PluginMetadata metadata,
      string entryPoint,
      string pluginPath,
      List<string> capabilities,
      Dictionary<string, object>? configuration = null)
  {
    Id = id;
    Metadata = metadata;
    EntryPoint = entryPoint;
    PluginPath = pluginPath;
    _capabilities.AddRange(capabilities);
    Configuration = configuration ?? new Dictionary<string, object>();
    Status = PluginStatus.Registered;
    RegisteredAt = DateTime.UtcNow;

    AddDomainEvent(new PluginRegisteredEvent(Id, Metadata.Name, Metadata.Language, RegisteredAt));
  }

  /// <summary>
  /// Gets the plugin metadata information.
  /// </summary>
  public PluginMetadata Metadata { get; private set; } = null!;

  /// <summary>
  /// Gets the plugin entry point (main file or class).
  /// </summary>
  public string EntryPoint { get; private set; } = null!;

  /// <summary>
  /// Gets the file system path to the plugin.
  /// </summary>
  public string PluginPath { get; private set; } = null!;

  /// <summary>
  /// Gets the plugin capabilities (permissions/features it requires).
  /// </summary>
  public IReadOnlyList<string> Capabilities => _capabilities.AsReadOnly();

  /// <summary>
  /// Gets the plugin dependencies.
  /// </summary>
  public IReadOnlyList<PluginDependency> Dependencies => _dependencies.AsReadOnly();

  /// <summary>
  /// Gets the plugin configuration settings.
  /// </summary>
  public Dictionary<string, object> Configuration { get; private set; } = null!;

  /// <summary>
  /// Gets the current plugin status.
  /// </summary>
  public PluginStatus Status { get; private set; }

  /// <summary>
  /// Gets the plugin registration timestamp.
  /// </summary>
  public DateTime RegisteredAt { get; private set; }

  /// <summary>
  /// Gets the last validation timestamp.
  /// </summary>
  public DateTime? LastValidatedAt { get; private set; }

  /// <summary>
  /// Gets the last execution timestamp.
  /// </summary>
  public DateTime? LastExecutedAt { get; private set; }

  /// <summary>
  /// Gets the total number of successful executions.
  /// </summary>
  public int ExecutionCount { get; private set; }

  /// <summary>
  /// Gets the validation or execution error message if plugin is in error state.
  /// </summary>
  public string? ErrorMessage { get; private set; }

  /// <summary>
  /// Gets the SHA256 hash of the plugin's critical source files (manifest and entry point).
  /// Used to detect changes for re-validation.
  /// </summary>
  public string? SourceHash { get; private set; }

  /// <summary>
  /// Updates the source hash of the plugin.
  /// </summary>
  /// <param name="newHash">The new SHA256 hash of the plugin's source files.</param>
  public void UpdateSourceHash(string newHash)
  {
    SourceHash = newHash;
  }


  /// <summary>
  /// Creates a new plugin with the specified details.
  /// </summary>
  public static Result<Plugin> Create(
      string name,
      string version,
      string description,
      PluginLanguage language,
      string entryPoint,
      string pluginPath,
      List<string>? capabilities = null,
      Dictionary<string, object>? configuration = null)
  {
    var metadataResult = PluginMetadata.Create(name, version, description, language);
    if (metadataResult.IsFailure)
      return Result<Plugin>.Failure(metadataResult.Error);

    if (string.IsNullOrWhiteSpace(entryPoint))
      return Result<Plugin>.Failure(Error.Validation(
          "Plugin.EntryPointEmpty", "Plugin entry point cannot be empty."));

    if (string.IsNullOrWhiteSpace(pluginPath))
      return Result<Plugin>.Failure(Error.Validation(
          "Plugin.PluginPathEmpty", "Plugin path cannot be empty."));

    var id = PluginId.New();
    var plugin = new Plugin(
        id,
        metadataResult.Value,
        entryPoint.Trim(),
        pluginPath.Trim(),
        capabilities ?? new List<string>(),
        configuration);

    return Result<Plugin>.Success(plugin);
  }

  /// <summary>
  /// Validates the plugin and updates its status.
  /// </summary>
  public Result Validate(bool isValid, string? errorMessage = null)
  {
    LastValidatedAt = DateTime.UtcNow;

    if (isValid)
    {
      Status = PluginStatus.Available;
      ErrorMessage = null;
      AddDomainEvent(new PluginValidatedEvent(Id, LastValidatedAt.Value, true));
    }
    else
    {
      Status = PluginStatus.Error;
      ErrorMessage = errorMessage ?? "Plugin validation failed.";
      AddDomainEvent(new PluginValidatedEvent(Id, LastValidatedAt.Value, false, ErrorMessage));
    }

    return Result.Success();
  }

  /// <summary>
  /// Records a successful plugin execution.
  /// </summary>
  public Result RecordExecution()
  {
    if (Status != PluginStatus.Available)
      return Result.Failure(Error.Validation(
          "Plugin.NotAvailable", "Cannot execute plugin that is not available."));

    LastExecutedAt = DateTime.UtcNow;
    ExecutionCount++;

    AddDomainEvent(new PluginExecutedEvent(Id, LastExecutedAt.Value, ExecutionCount));

    return Result.Success();
  }

  /// <summary>
  /// Updates the plugin configuration.
  /// </summary>
  public Result UpdateConfiguration(Dictionary<string, object> configuration)
  {
    Configuration = configuration ?? new Dictionary<string, object>();
    AddDomainEvent(new PluginConfigurationUpdatedEvent(Id, DateTime.UtcNow));
    return Result.Success();
  }

  /// <summary>
  /// Disables the plugin.
  /// </summary>
  public Result Disable(string? reason = null)
  {
    if (Status == PluginStatus.Disabled)
      return Result.Success(); // Already disabled

    Status = PluginStatus.Disabled;
    ErrorMessage = reason;

    AddDomainEvent(new PluginDisabledEvent(Id, DateTime.UtcNow, reason));

    return Result.Success();
  }

  /// <summary>
  /// Enables the plugin (requires re-validation).
  /// </summary>
  public Result Enable()
  {
    if (Status != PluginStatus.Disabled)
      return Result.Failure(Error.Validation(
          "Plugin.NotDisabled", "Cannot enable plugin that is not disabled."));

    Status = PluginStatus.Registered;
    ErrorMessage = null;

    AddDomainEvent(new PluginEnabledEvent(Id, DateTime.UtcNow));

    return Result.Success();
  }

  /// <summary>
  /// Adds a dependency to the plugin.
  /// </summary>
  /// <param name="dependency">The dependency to add</param>
  /// <returns>A result indicating success or failure</returns>
  public Result AddDependency(PluginDependency dependency)
  {
    if (dependency is null)
      return Result.Failure(Error.Validation(
          "Plugin.DependencyNull", "Dependency cannot be null."));

    // Check for duplicate dependencies
    if (_dependencies.Any(d => d.MatchesName(dependency.Name) && d.Type == dependency.Type))
      return Result.Failure(Error.Validation(
          "Plugin.DuplicateDependency", 
          $"Dependency '{dependency.Name}' of type '{dependency.Type}' already exists."));

    _dependencies.Add(dependency);
    AddDomainEvent(new PluginDependencyAddedEvent(Id, dependency));
    
    return Result.Success();
  }

  /// <summary>
  /// Removes a dependency from the plugin.
  /// </summary>
  /// <param name="dependencyName">The name of the dependency to remove</param>
  /// <param name="dependencyType">The type of the dependency to remove</param>
  /// <returns>A result indicating success or failure</returns>
  public Result RemoveDependency(string dependencyName, PluginDependencyType dependencyType)
  {
    if (string.IsNullOrWhiteSpace(dependencyName))
      return Result.Failure(Error.Validation(
          "Plugin.DependencyNameEmpty", "Dependency name cannot be empty."));

    var dependency = _dependencies.FirstOrDefault(d => 
        d.MatchesName(dependencyName) && d.Type == dependencyType);

    if (dependency is null)
      return Result.Failure(Error.NotFound(
          "Plugin.DependencyNotFound", 
          $"Dependency '{dependencyName}' of type '{dependencyType}' not found."));

    _dependencies.Remove(dependency);
    AddDomainEvent(new PluginDependencyRemovedEvent(Id, dependency));
    
    return Result.Success();
  }

  /// <summary>
  /// Gets all dependencies of a specific type.
  /// </summary>
  /// <param name="dependencyType">The type of dependencies to retrieve</param>
  /// <returns>A read-only list of dependencies</returns>
  public IReadOnlyList<PluginDependency> GetDependenciesByType(PluginDependencyType dependencyType)
  {
    return _dependencies.Where(d => d.Type == dependencyType).ToList().AsReadOnly();
  }

  /// <summary>
  /// Checks if the plugin has any dependencies of the specified type.
  /// </summary>
  /// <param name="dependencyType">The type of dependency to check for</param>
  /// <returns>True if the plugin has dependencies of the specified type, false otherwise</returns>
  public bool HasDependenciesOfType(PluginDependencyType dependencyType)
  {
    return _dependencies.Any(d => d.Type == dependencyType);
  }

  /// <summary>
  /// Gets a specific dependency by name and type.
  /// </summary>
  /// <param name="dependencyName">The name of the dependency</param>
  /// <param name="dependencyType">The type of the dependency</param>
  /// <returns>The dependency if found, null otherwise</returns>
  public PluginDependency? GetDependency(string dependencyName, PluginDependencyType dependencyType)
  {
    return _dependencies.FirstOrDefault(d => 
        d.MatchesName(dependencyName) && d.Type == dependencyType);
  }

  /// <summary>
  /// Updates the plugin dependencies from a list.
  /// </summary>
  /// <param name="dependencies">The new list of dependencies</param>
  /// <returns>A result indicating success or failure</returns>
  public Result UpdateDependencies(IEnumerable<PluginDependency> dependencies)
  {
    if (dependencies is null)
      return Result.Failure(Error.Validation(
          "Plugin.DependenciesNull", "Dependencies list cannot be null."));

    var dependencyList = dependencies.ToList();
    
    // Validate no duplicate dependencies
    var duplicates = dependencyList
        .GroupBy(d => new { d.Name, d.Type })
        .Where(g => g.Count() > 1)
        .Select(g => g.Key)
        .ToList();
    
    if (duplicates.Any())
    {
      var duplicateNames = string.Join(", ", duplicates.Select(d => $"{d.Name} ({d.Type})"));
      return Result.Failure(Error.Validation(
          "Plugin.DuplicateDependencies", 
          $"Duplicate dependencies found: {duplicateNames}"));
    }

    _dependencies.Clear();
    _dependencies.AddRange(dependencyList);
    
    AddDomainEvent(new PluginDependenciesUpdatedEvent(Id, dependencyList.AsReadOnly()));
    
    return Result.Success();
  }
}
