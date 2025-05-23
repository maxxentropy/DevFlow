using DevFlow.Domain.Common;
using DevFlow.Domain.Plugins.ValueObjects;
using DevFlow.Domain.Plugins.Enums;  
using DevFlow.Domain.Plugins.Events;
using DevFlow.SharedKernel.Common;

namespace DevFlow.Domain.Plugins.Entities;

/// <summary>
/// Represents a plugin that can be executed within a workflow.
/// </summary>
public sealed class Plugin : AggregateRoot<PluginId>
{
    private readonly List<string> _capabilities = new();

    // Private constructor for persistence
    private Plugin(PluginId id) : base(id) { }

    // Constructor for creating new plugins
    private Plugin(
        PluginId id,
        PluginMetadata metadata,
        string entryPoint,
        string pluginPath,
        List<string> capabilities,
        Dictionary<string, object>? configuration = null) : base(id)
    {
        Metadata = metadata;
        EntryPoint = entryPoint;
        PluginPath = pluginPath;
        _capabilities.AddRange(capabilities);
        Configuration = configuration ?? new Dictionary<string, object>();
        Status = PluginStatus.Registered;
        RegisteredAt = DateTime.UtcNow;

        RaiseDomainEvent(new PluginRegisteredEvent(Id, Metadata.Name, Metadata.Language, RegisteredAt));
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
            RaiseDomainEvent(new PluginValidatedEvent(Id, LastValidatedAt.Value, true));
        }
        else
        {
            Status = PluginStatus.Error;
            ErrorMessage = errorMessage ?? "Plugin validation failed.";
            RaiseDomainEvent(new PluginValidatedEvent(Id, LastValidatedAt.Value, false, ErrorMessage));
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

        RaiseDomainEvent(new PluginExecutedEvent(Id, LastExecutedAt.Value, ExecutionCount));

        return Result.Success();
    }

    /// <summary>
    /// Updates the plugin configuration.
    /// </summary>
    public Result UpdateConfiguration(Dictionary<string, object> configuration)
    {
        Configuration = configuration ?? new Dictionary<string, object>();
        RaiseDomainEvent(new PluginConfigurationUpdatedEvent(Id, DateTime.UtcNow));
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

        RaiseDomainEvent(new PluginDisabledEvent(Id, DateTime.UtcNow, reason));

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

        RaiseDomainEvent(new PluginEnabledEvent(Id, DateTime.UtcNow));

        return Result.Success();
    }
}