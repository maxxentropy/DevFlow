using DevFlow.Domain.Common;
using DevFlow.Domain.Plugins.Enums;
using MediatR;

namespace DevFlow.Domain.Plugins.Events;

/// <summary>
/// Domain event raised when a plugin is registered.
/// </summary>
/// <param name="PluginId">The plugin identifier</param>
/// <param name="PluginName">The plugin name</param>
/// <param name="Language">The plugin language</param>
/// <param name="RegisteredAt">The registration timestamp</param>
public sealed record PluginRegisteredEvent(
    PluginId PluginId,
    string PluginName,
    PluginLanguage Language,
    DateTime RegisteredAt) : INotification;

/// <summary>
/// Domain event raised when a plugin is validated.
/// </summary>
/// <param name="PluginId">The plugin identifier</param>
/// <param name="ValidatedAt">The validation timestamp</param>
/// <param name="IsValid">Whether validation was successful</param>
/// <param name="ErrorMessage">The error message if validation failed</param>
public sealed record PluginValidatedEvent(
    PluginId PluginId,
    DateTime ValidatedAt,
    bool IsValid,
    string? ErrorMessage = null) : INotification;

/// <summary>
/// Domain event raised when a plugin is executed.
/// </summary>
/// <param name="PluginId">The plugin identifier</param>
/// <param name="ExecutedAt">The execution timestamp</param>
/// <param name="ExecutionCount">The total execution count</param>
public sealed record PluginExecutedEvent(
    PluginId PluginId,
    DateTime ExecutedAt,
    int ExecutionCount) : INotification;

/// <summary>
/// Domain event raised when a plugin configuration is updated.
/// </summary>
/// <param name="PluginId">The plugin identifier</param>
/// <param name="UpdatedAt">The update timestamp</param>
public sealed record PluginConfigurationUpdatedEvent(
    PluginId PluginId,
    DateTime UpdatedAt) : INotification;

/// <summary>
/// Domain event raised when a plugin is disabled.
/// </summary>
/// <param name="PluginId">The plugin identifier</param>
/// <param name="DisabledAt">The disable timestamp</param>
/// <param name="Reason">The reason for disabling</param>
public sealed record PluginDisabledEvent(
    PluginId PluginId,
    DateTime DisabledAt,
    string? Reason) : INotification;

/// <summary>
/// Domain event raised when a plugin is enabled.
/// </summary>
/// <param name="PluginId">The plugin identifier</param>
/// <param name="EnabledAt">The enable timestamp</param>
public sealed record PluginEnabledEvent(
    PluginId PluginId,
    DateTime EnabledAt) : INotification;