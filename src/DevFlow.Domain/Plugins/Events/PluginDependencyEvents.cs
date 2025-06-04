using DevFlow.Domain.Common;
using DevFlow.Domain.Plugins.ValueObjects;
using DevFlow.SharedKernel.Events;

namespace DevFlow.Domain.Plugins.Events;

/// <summary>
/// Domain event raised when a dependency is added to a plugin.
/// </summary>
public sealed record PluginDependencyAddedEvent(
    PluginId PluginId,
    PluginDependency Dependency) : DomainEvent
{
    public string EventType => "PluginDependencyAdded";
    
    public string GetDescription() => 
        $"Dependency '{Dependency.Name}' of type '{Dependency.Type}' added to plugin {PluginId.Value}";
}

/// <summary>
/// Domain event raised when a dependency is removed from a plugin.
/// </summary>
public sealed record PluginDependencyRemovedEvent(
    PluginId PluginId,
    PluginDependency Dependency) : DomainEvent
{
    public string EventType => "PluginDependencyRemoved";
    
    public string GetDescription() => 
        $"Dependency '{Dependency.Name}' of type '{Dependency.Type}' removed from plugin {PluginId.Value}";
}

/// <summary>
/// Domain event raised when plugin dependencies are updated.
/// </summary>
public sealed record PluginDependenciesUpdatedEvent(
    PluginId PluginId,
    IReadOnlyList<PluginDependency> Dependencies) : DomainEvent
{
    public string EventType => "PluginDependenciesUpdated";
    
    public string GetDescription() => 
        $"Dependencies updated for plugin {PluginId.Value}. Total dependencies: {Dependencies.Count}";
}

