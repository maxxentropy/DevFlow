using DevFlow.Domain.Plugins.Enums;
using DevFlow.SharedKernel.Common;

namespace DevFlow.Domain.Plugins.ValueObjects;

/// <summary>
/// Represents metadata information about a plugin.
/// </summary>
public sealed class PluginMetadata : ValueObject
{
    private PluginMetadata(string name, Version version, string description, PluginLanguage language)
    {
        Name = name;
        Version = version;
        Description = description;
        Language = language;
    }

    /// <summary>
    /// Gets the plugin name.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Gets the plugin version.
    /// </summary>
    public Version Version { get; private set; }

    /// <summary>
    /// Gets the plugin description.
    /// </summary>
    public string Description { get; private set; }

    /// <summary>
    /// Gets the plugin language.
    /// </summary>
    public PluginLanguage Language { get; private set; }

    /// <summary>
    /// Creates new plugin metadata with validation.
    /// </summary>
    public static Result<PluginMetadata> Create(string name, string version, string description, PluginLanguage language)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result<PluginMetadata>.Failure(Error.Validation(
                "PluginMetadata.NameEmpty", "Plugin name cannot be empty."));

        if (string.IsNullOrWhiteSpace(version))
            return Result<PluginMetadata>.Failure(Error.Validation(
                "PluginMetadata.VersionEmpty", "Plugin version cannot be empty."));

        if (!Version.TryParse(version, out var parsedVersion))
            return Result<PluginMetadata>.Failure(Error.Validation(
                "PluginMetadata.InvalidVersion", "Plugin version must be a valid semantic version."));

        var trimmedDescription = description?.Trim() ?? string.Empty;

        return Result<PluginMetadata>.Success(new PluginMetadata(
            name.Trim(), 
            parsedVersion, 
            trimmedDescription, 
            language));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Name;
        yield return Version;
        yield return Description;
        yield return Language;
    }
}