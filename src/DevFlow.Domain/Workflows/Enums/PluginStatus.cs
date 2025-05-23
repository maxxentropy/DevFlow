namespace DevFlow.Domain.Plugins.Enums;

/// <summary>
/// Represents the possible statuses of a plugin.
/// </summary>
public enum PluginStatus
{
    /// <summary>
    /// The plugin has been registered but not yet validated.
    /// </summary>
    Registered = 0,

    /// <summary>
    /// The plugin has been validated and is available for execution.
    /// </summary>
    Available = 1,

    /// <summary>
    /// The plugin has validation or runtime errors.
    /// </summary>
    Error = 2,

    /// <summary>
    /// The plugin has been manually disabled.
    /// </summary>
    Disabled = 3
}