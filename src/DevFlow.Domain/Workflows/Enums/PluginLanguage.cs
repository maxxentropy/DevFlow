namespace DevFlow.Domain.Plugins.Enums;

/// <summary>
/// Represents the supported plugin development languages.
/// </summary>
public enum PluginLanguage
{
    /// <summary>
    /// C# plugins executed in-process or via assembly loading.
    /// </summary>
    CSharp = 0,

    /// <summary>
    /// TypeScript/JavaScript plugins executed via Node.js runtime.
    /// </summary>
    TypeScript = 1,

    /// <summary>
    /// Python plugins executed via Python interpreter.
    /// </summary>
    Python = 2
}