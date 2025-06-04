using DevFlow.Application.Plugins;
using DevFlow.Application.Plugins.Runtime;
using DevFlow.Application.Plugins.Runtime.Models;
using DevFlow.Domain.Common;
using DevFlow.Domain.Plugins.Entities;
using DevFlow.Domain.Plugins.Enums;
using DevFlow.Domain.Plugins.ValueObjects;
using DevFlow.SharedKernel.Results;
using Microsoft.Extensions.Logging;
using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;

namespace DevFlow.Infrastructure.Plugins;

/// <summary>
/// Default implementation of plugin dependency resolver.
/// Handles NuGet packages, plugin-to-plugin dependencies, and file references.
/// </summary>
public sealed class PluginDependencyResolver : IPluginDependencyResolver
{
    private readonly IPluginRepository _pluginRepository;
    private readonly ILogger<PluginDependencyResolver> _logger;
    private readonly string _dependencyCachePath;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public PluginDependencyResolver(
        IPluginRepository pluginRepository,
        ILogger<PluginDependencyResolver> logger,
        HttpClient httpClient)
    {
        _pluginRepository = pluginRepository;
        _logger = logger;
        _httpClient = httpClient;
        _dependencyCachePath = Path.Combine(Path.GetTempPath(), "devflow-plugin-dependencies");
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        // Ensure cache directory exists
        Directory.CreateDirectory(_dependencyCachePath);
    }

    public async Task<Result<PluginDependencyContext>> ResolveDependenciesAsync(
        Plugin plugin,
        CancellationToken cancellationToken = default)
    {
        if (plugin is null)
            return Result<PluginDependencyContext>.Failure(Error.Validation(
                "PluginDependencyResolver.PluginNull", "Plugin cannot be null."));

        _logger.LogInformation("Resolving dependencies for plugin: {PluginName}", plugin.Metadata.Name);

        try
        {
            var nugetPackages = new List<ResolvedNuGetPackage>();
            var pluginReferences = new List<ResolvedPluginReference>();
            var fileReferences = new List<ResolvedFileReference>();
            var warnings = new List<string>();

            // Resolve NuGet package dependencies
            var nugetDependencies = plugin.GetDependenciesByType(PluginDependencyType.NuGetPackage);
            foreach (var dependency in nugetDependencies)
            {
                var packageResult = await ResolveNuGetPackageAsync(dependency, "net8.0", cancellationToken);
                if (packageResult.IsSuccess)
                {
                    nugetPackages.Add(packageResult.Value);
                }
                else
                {
                    warnings.Add($"Failed to resolve NuGet package '{dependency.Name}': {packageResult.Error.Message}");
                }
            }

            // Resolve plugin-to-plugin dependencies
            var pluginDependencies = plugin.GetDependenciesByType(PluginDependencyType.Plugin);
            foreach (var dependency in pluginDependencies)
            {
                var pluginResult = await ResolvePluginDependencyAsync(dependency, cancellationToken);
                if (pluginResult.IsSuccess)
                {
                    pluginReferences.Add(pluginResult.Value);
                }
                else
                {
                    warnings.Add($"Failed to resolve plugin dependency '{dependency.Name}': {pluginResult.Error.Message}");
                }
            }

            // Resolve file reference dependencies
            var fileDependencies = plugin.GetDependenciesByType(PluginDependencyType.FileReference);
            foreach (var dependency in fileDependencies)
            {
                var fileResult = await ResolveFileReferenceAsync(dependency, plugin.PluginPath, cancellationToken);
                if (fileResult.IsSuccess)
                {
                    fileReferences.Add(fileResult.Value);
                }
                else
                {
                    warnings.Add($"Failed to resolve file reference '{dependency.Name}': {fileResult.Error.Message}");
                }
            }

            var context = new PluginDependencyContext
            {
                PluginId = plugin.Id,
                NuGetPackages = nugetPackages.AsReadOnly(),
                PluginReferences = pluginReferences.AsReadOnly(),
                FileReferences = fileReferences.AsReadOnly(),
                ResolvedAt = DateTimeOffset.UtcNow,
                Warnings = warnings.AsReadOnly()
            };

            _logger.LogInformation("Resolved {NuGetCount} NuGet packages, {PluginCount} plugin references, {FileCount} file references for plugin: {PluginName}",
                nugetPackages.Count, pluginReferences.Count, fileReferences.Count, plugin.Metadata.Name);

            return Result<PluginDependencyContext>.Success(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve dependencies for plugin: {PluginName}", plugin.Metadata.Name);
            return Result<PluginDependencyContext>.Failure(Error.Failure(
                "PluginDependencyResolver.ResolutionFailed", $"Dependency resolution failed: {ex.Message}"));
        }
    }

    public async Task<Result<PluginDependencyValidation>> ValidateDependenciesAsync(
        Plugin plugin,
        CancellationToken cancellationToken = default)
    {
        if (plugin is null)
            return Result<PluginDependencyValidation>.Failure(Error.Validation(
                "PluginDependencyResolver.PluginNull", "Plugin cannot be null."));

        _logger.LogDebug("Validating dependencies for plugin: {PluginName}", plugin.Metadata.Name);

        try
        {
            var issues = new List<DependencyValidationIssue>();
            var totalDependencies = plugin.Dependencies.Count;
            var resolvedCount = 0;

            foreach (var dependency in plugin.Dependencies)
            {
                var validationResult = await ValidateSingleDependencyAsync(dependency, plugin.PluginPath, cancellationToken);
                if (validationResult.IsSuccess)
                {
                    resolvedCount++;
                }
                else
                {
                    issues.Add(new DependencyValidationIssue
                    {
                        Severity = DependencyIssueSeverity.Error,
                        Dependency = dependency,
                        Message = validationResult.Error.Message,
                        SuggestedResolution = GetSuggestedResolution(dependency)
                    });
                }
            }

            var validation = issues.Any()
                ? PluginDependencyValidation.Failed(issues, totalDependencies, resolvedCount)
                : PluginDependencyValidation.Success(totalDependencies);

            _logger.LogDebug("Dependency validation completed for plugin: {PluginName}. Valid: {IsValid}, Total: {Total}, Resolved: {Resolved}",
                plugin.Metadata.Name, validation.IsValid, totalDependencies, resolvedCount);

            return Result<PluginDependencyValidation>.Success(validation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate dependencies for plugin: {PluginName}", plugin.Metadata.Name);
            return Result<PluginDependencyValidation>.Failure(Error.Failure(
                "PluginDependencyResolver.ValidationFailed", $"Dependency validation failed: {ex.Message}"));
        }
    }

    public async Task<Result<ResolvedNuGetPackage>> ResolveNuGetPackageAsync(
        PluginDependency dependency,
        string targetFramework = "net8.0",
        CancellationToken cancellationToken = default)
    {
        if (dependency?.Type != PluginDependencyType.NuGetPackage)
            return Result<ResolvedNuGetPackage>.Failure(Error.Validation(
                "PluginDependencyResolver.InvalidDependencyType", "Dependency must be a NuGet package."));

        _logger.LogDebug("Resolving NuGet package: {PackageName} v{Version}", dependency.Name, dependency.Version);

        try
        {
            // Check cache first
            var cacheKey = $"{dependency.Name}.{dependency.Version}.{targetFramework}";
            var cachePath = Path.Combine(_dependencyCachePath, cacheKey);
            
            if (Directory.Exists(cachePath))
            {
                _logger.LogDebug("Found cached package: {PackageName}", dependency.Name);
                return Result<ResolvedNuGetPackage>.Success(LoadCachedPackage(dependency, cachePath, targetFramework));
            }

            // Download and extract package
            var downloadResult = await DownloadNuGetPackageAsync(dependency.Name, dependency.Version, targetFramework, cancellationToken);
            if (downloadResult.IsFailure)
                return Result<ResolvedNuGetPackage>.Failure(downloadResult.Error);

            var packagePath = downloadResult.Value;
            return Result<ResolvedNuGetPackage>.Success(LoadCachedPackage(dependency, packagePath, targetFramework));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve NuGet package: {PackageName}", dependency.Name);
            return Result<ResolvedNuGetPackage>.Failure(Error.Failure(
                "PluginDependencyResolver.NuGetResolutionFailed", $"NuGet package resolution failed: {ex.Message}"));
        }
    }

    public async Task<Result<ResolvedPluginReference>> ResolvePluginDependencyAsync(
        PluginDependency dependency,
        CancellationToken cancellationToken = default)
    {
        if (dependency?.Type != PluginDependencyType.Plugin)
            return Result<ResolvedPluginReference>.Failure(Error.Validation(
                "PluginDependencyResolver.InvalidDependencyType", "Dependency must be a plugin reference."));

        _logger.LogDebug("Resolving plugin dependency: {PluginName} v{Version}", dependency.Name, dependency.Version);

        try
        {
            var plugins = await _pluginRepository.GetAllAsync(cancellationToken);
            var matchingPlugin = plugins.FirstOrDefault(p => 
                p.Metadata.Name.Equals(dependency.Name, StringComparison.OrdinalIgnoreCase) &&
                dependency.IsVersionSatisfied(p.Metadata.Version.ToString()));

            if (matchingPlugin is null)
            {
                return Result<ResolvedPluginReference>.Failure(Error.NotFound(
                    "PluginDependencyResolver.PluginNotFound",
                    $"Plugin '{dependency.Name}' with version constraint '{dependency.Version}' not found."));
            }

            var reference = new ResolvedPluginReference
            {
                PluginId = matchingPlugin.Id,
                PluginName = matchingPlugin.Metadata.Name,
                Version = matchingPlugin.Metadata.Version.ToString(),
                PluginPath = matchingPlugin.PluginPath,
                EntryPoint = matchingPlugin.EntryPoint,
                IsAvailable = matchingPlugin.Status == PluginStatus.Available
            };

            _logger.LogDebug("Resolved plugin dependency: {PluginName} -> {ResolvedPlugin} v{ResolvedVersion}",
                dependency.Name, reference.PluginName, reference.Version);

            return Result<ResolvedPluginReference>.Success(reference);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve plugin dependency: {PluginName}", dependency.Name);
            return Result<ResolvedPluginReference>.Failure(Error.Failure(
                "PluginDependencyResolver.PluginResolutionFailed", $"Plugin dependency resolution failed: {ex.Message}"));
        }
    }

    public async Task<Result<ResolvedFileReference>> ResolveFileReferenceAsync(
        PluginDependency dependency,
        string baseDirectory,
        CancellationToken cancellationToken = default)
    {
        if (dependency?.Type != PluginDependencyType.FileReference)
            return Result<ResolvedFileReference>.Failure(Error.Validation(
                "PluginDependencyResolver.InvalidDependencyType", "Dependency must be a file reference."));

        _logger.LogDebug("Resolving file reference: {FileName}", dependency.Name);

        try
        {
            var filePath = dependency.Source;
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return Result<ResolvedFileReference>.Failure(Error.Validation(
                    "PluginDependencyResolver.FilePathEmpty", "File reference source path cannot be empty."));
            }

            // Resolve relative paths relative to the plugin directory
            if (!Path.IsPathRooted(filePath))
            {
                filePath = Path.Combine(baseDirectory, filePath);
            }

            filePath = Path.GetFullPath(filePath);
            var fileExists = File.Exists(filePath);
            var fileInfo = fileExists ? new FileInfo(filePath) : null;

            var reference = new ResolvedFileReference
            {
                FileName = dependency.Name,
                FilePath = filePath,
                Version = dependency.Version,
                Exists = fileExists,
                FileSize = fileInfo?.Length ?? 0,
                LastModified = fileInfo?.LastWriteTimeUtc ?? DateTimeOffset.MinValue
            };

            if (!fileExists)
            {
                _logger.LogWarning("File reference does not exist: {FilePath}", filePath);
            }

            return Result<ResolvedFileReference>.Success(reference);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve file reference: {FileName}", dependency.Name);
            return Result<ResolvedFileReference>.Failure(Error.Failure(
                "PluginDependencyResolver.FileResolutionFailed", $"File reference resolution failed: {ex.Message}"));
        }
    }

    public async Task<Result<string>> DownloadNuGetPackageAsync(
        string packageName,
        string version,
        string targetFramework = "net8.0",
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Downloading NuGet package: {PackageName} v{Version}", packageName, version);

        try
        {
            var cacheKey = $"{packageName}.{version}.{targetFramework}";
            var cachePath = Path.Combine(_dependencyCachePath, cacheKey);
            
            if (Directory.Exists(cachePath))
            {
                _logger.LogDebug("Package already cached: {PackageName}", packageName);
                return Result<string>.Success(cachePath);
            }

            // For now, implement a basic mock download
            // In a real implementation, this would use NuGet.Protocol to download from nuget.org
            Directory.CreateDirectory(cachePath);
            
            // Create a mock assembly file for testing
            var mockAssemblyPath = Path.Combine(cachePath, $"{packageName}.dll");
            await File.WriteAllTextAsync(mockAssemblyPath, $"// Mock assembly for {packageName} v{version}", cancellationToken);
            
            _logger.LogInformation("Mock downloaded NuGet package: {PackageName} v{Version} to {CachePath}", 
                packageName, version, cachePath);

            return Result<string>.Success(cachePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download NuGet package: {PackageName}", packageName);
            return Result<string>.Failure(Error.Failure(
                "PluginDependencyResolver.DownloadFailed", $"Package download failed: {ex.Message}"));
        }
    }

    public async Task ClearDependencyCacheAsync(PluginId pluginId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Clearing dependency cache for plugin: {PluginId}", pluginId.Value);

        try
        {
            var pluginCachePath = Path.Combine(_dependencyCachePath, pluginId.Value.ToString());
            if (Directory.Exists(pluginCachePath))
            {
                Directory.Delete(pluginCachePath, true);
                _logger.LogDebug("Cleared dependency cache: {CachePath}", pluginCachePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear dependency cache for plugin: {PluginId}", pluginId.Value);
        }
    }

    public async Task<Result<PluginDependencyGraph>> GetDependencyGraphAsync(
        Plugin plugin,
        bool includeTransitive = true,
        CancellationToken cancellationToken = default)
    {
        if (plugin is null)
            return Result<PluginDependencyGraph>.Failure(Error.Validation(
                "PluginDependencyResolver.PluginNull", "Plugin cannot be null."));

        _logger.LogDebug("Building dependency graph for plugin: {PluginName}", plugin.Metadata.Name);

        try
        {
            var visited = new HashSet<string>();
            var directNodes = new List<DependencyGraphNode>();
            var allNodes = new List<DependencyGraphNode>();
            var warnings = new List<string>();
            var maxDepth = 0;

            foreach (var dependency in plugin.Dependencies)
            {
                var node = await BuildDependencyNodeAsync(dependency, 0, visited, includeTransitive, cancellationToken);
                directNodes.Add(node);
                CollectAllNodes(node, allNodes);
                maxDepth = Math.Max(maxDepth, GetNodeDepth(node));
            }

            var graph = new PluginDependencyGraph
            {
                RootPluginId = plugin.Id,
                DirectDependencies = directNodes.AsReadOnly(),
                AllDependencies = allNodes.AsReadOnly(),
                CircularDependencyWarnings = warnings.AsReadOnly(),
                MaxDepth = maxDepth
            };

            _logger.LogDebug("Built dependency graph for plugin: {PluginName}. Nodes: {NodeCount}, Max Depth: {MaxDepth}",
                plugin.Metadata.Name, allNodes.Count, maxDepth);

            return Result<PluginDependencyGraph>.Success(graph);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build dependency graph for plugin: {PluginName}", plugin.Metadata.Name);
            return Result<PluginDependencyGraph>.Failure(Error.Failure(
                "PluginDependencyResolver.GraphBuildFailed", $"Dependency graph building failed: {ex.Message}"));
        }
    }

    private async Task<Result<bool>> ValidateSingleDependencyAsync(
        PluginDependency dependency,
        string baseDirectory,
        CancellationToken cancellationToken)
    {
        return dependency.Type switch
        {
            PluginDependencyType.NuGetPackage => await ValidateNuGetPackageAsync(dependency, cancellationToken),
            PluginDependencyType.Plugin => await ValidatePluginDependencyAsync(dependency, cancellationToken),
            PluginDependencyType.FileReference => ValidateFileReference(dependency, baseDirectory),
            _ => Result<bool>.Failure(Error.Validation(
                "PluginDependencyResolver.UnsupportedType", $"Unsupported dependency type: {dependency.Type}"))
        };
    }

    private async Task<Result<bool>> ValidateNuGetPackageAsync(PluginDependency dependency, CancellationToken cancellationToken)
    {
        // For now, just check if the package name is valid
        // In a real implementation, this would query NuGet API
        var isValid = !string.IsNullOrWhiteSpace(dependency.Name) && !string.IsNullOrWhiteSpace(dependency.Version);
        return Result<bool>.Success(isValid);
    }

    private async Task<Result<bool>> ValidatePluginDependencyAsync(PluginDependency dependency, CancellationToken cancellationToken)
    {
        var plugins = await _pluginRepository.GetAllAsync(cancellationToken);
        var exists = plugins.Any(p => 
            p.Metadata.Name.Equals(dependency.Name, StringComparison.OrdinalIgnoreCase) &&
            dependency.IsVersionSatisfied(p.Metadata.Version.ToString()));
        
        return Result<bool>.Success(exists);
    }

    private static Result<bool> ValidateFileReference(PluginDependency dependency, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(dependency.Source))
            return Result<bool>.Failure(Error.Validation(
                "PluginDependencyResolver.FilePathEmpty", "File reference source path cannot be empty."));

        var filePath = dependency.Source;
        if (!Path.IsPathRooted(filePath))
        {
            filePath = Path.Combine(baseDirectory, filePath);
        }

        var exists = File.Exists(Path.GetFullPath(filePath));
        return Result<bool>.Success(exists);
    }

    private static string GetSuggestedResolution(PluginDependency dependency)
    {
        return dependency.Type switch
        {
            PluginDependencyType.NuGetPackage => "Ensure the NuGet package name and version are correct and the package exists on nuget.org",
            PluginDependencyType.Plugin => "Ensure the referenced plugin is registered and available in the system",
            PluginDependencyType.FileReference => "Ensure the file path is correct and the file exists",
            _ => "Check the dependency configuration"
        };
    }

    private ResolvedNuGetPackage LoadCachedPackage(PluginDependency dependency, string packagePath, string targetFramework)
    {
        var assemblies = Directory.GetFiles(packagePath, "*.dll", SearchOption.AllDirectories);
        
        return new ResolvedNuGetPackage
        {
            PackageName = dependency.Name,
            Version = dependency.Version,
            AssemblyPaths = assemblies.ToList().AsReadOnly(),
            RuntimeDependencies = Array.Empty<string>(),
            InstallationPath = packagePath,
            TargetFramework = targetFramework,
            Metadata = new Dictionary<string, string>
            {
                ["CachedAt"] = DateTimeOffset.UtcNow.ToString("O")
            }
        };
    }

    private async Task<DependencyGraphNode> BuildDependencyNodeAsync(
        PluginDependency dependency,
        int depth,
        HashSet<string> visited,
        bool includeTransitive,
        CancellationToken cancellationToken)
    {
        var nodeKey = $"{dependency.Type}:{dependency.Name}";
        var isResolved = true;
        string? errorMessage = null;
        var children = new List<DependencyGraphNode>();

        if (visited.Contains(nodeKey))
        {
            return new DependencyGraphNode
            {
                Dependency = dependency,
                Depth = depth,
                Children = Array.Empty<DependencyGraphNode>(),
                IsResolved = false,
                ErrorMessage = "Circular dependency detected"
            };
        }

        visited.Add(nodeKey);

        try
        {
            // For plugin dependencies, we can resolve transitive dependencies
            if (includeTransitive && dependency.Type == PluginDependencyType.Plugin)
            {
                var pluginResult = await ResolvePluginDependencyAsync(dependency, cancellationToken);
                if (pluginResult.IsSuccess)
                {
                    var referencedPlugin = await _pluginRepository.GetByIdAsync(pluginResult.Value.PluginId, cancellationToken);
                    if (referencedPlugin != null)
                    {
                        foreach (var childDependency in referencedPlugin.Dependencies)
                        {
                            var childNode = await BuildDependencyNodeAsync(childDependency, depth + 1, visited, includeTransitive, cancellationToken);
                            children.Add(childNode);
                        }
                    }
                }
                else
                {
                    isResolved = false;
                    errorMessage = pluginResult.Error.Message;
                }
            }
        }
        catch (Exception ex)
        {
            isResolved = false;
            errorMessage = ex.Message;
        }
        finally
        {
            visited.Remove(nodeKey);
        }

        return new DependencyGraphNode
        {
            Dependency = dependency,
            Depth = depth,
            Children = children.AsReadOnly(),
            IsResolved = isResolved,
            ErrorMessage = errorMessage
        };
    }

    private static void CollectAllNodes(DependencyGraphNode node, List<DependencyGraphNode> allNodes)
    {
        allNodes.Add(node);
        foreach (var child in node.Children)
        {
            CollectAllNodes(child, allNodes);
        }
    }

    private static int GetNodeDepth(DependencyGraphNode node)
    {
        if (!node.Children.Any())
            return node.Depth;

        return Math.Max(node.Depth, node.Children.Max(GetNodeDepth));
    }
}

