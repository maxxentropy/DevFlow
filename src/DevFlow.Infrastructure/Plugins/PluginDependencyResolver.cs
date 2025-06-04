// File: src/DevFlow.Infrastructure/Plugins/PluginDependencyResolver.cs
using DevFlow.Application.Plugins;
using DevFlow.Application.Plugins.Runtime;
using DevFlow.Application.Plugins.Runtime.Models;
using DevFlow.Domain.Common;
using DevFlow.Domain.Plugins.Entities;
using DevFlow.Domain.Plugins.Enums;
using DevFlow.Domain.Plugins.ValueObjects;
using DevFlow.SharedKernel.Results; // Assuming this is where your Error record with factories is
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
// For production NuGet interaction, consider official libraries:
// using NuGet.Common;
// using NuGet.Protocol;
// using NuGet.Protocol.Core.Types;
// using NuGet.Versioning;

namespace DevFlow.Infrastructure.Plugins;

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
    _pluginRepository = pluginRepository ?? throw new ArgumentNullException(nameof(pluginRepository));
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    _dependencyCachePath = Path.Combine(Path.GetTempPath(), "DevFlow", "PluginDependenciesCache");
    _jsonOptions = new JsonSerializerOptions
    {
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
      WriteIndented = true
    };

    try
    {
      Directory.CreateDirectory(_dependencyCachePath);
      _logger.LogInformation("Plugin dependency cache path initialized at: {CachePath}", _dependencyCachePath);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to create or access plugin dependency cache path: {CachePath}. Caching may be impaired.", _dependencyCachePath);
    }
  }

  public async Task<Result<PluginDependencyContext>> ResolveDependenciesAsync(
      Plugin plugin,
      CancellationToken cancellationToken = default)
  {
    if (plugin is null)
      return Result<PluginDependencyContext>.Failure(Error.Validation(
          "PluginDependencyResolver.PluginNull", "Plugin cannot be null."));

    _logger.LogInformation("Resolving dependencies for plugin: {PluginName} v{PluginVersion}",
        plugin.Metadata.Name, plugin.Metadata.Version);

    try
    {
      var nugetPackages = new List<ResolvedNuGetPackage>();
      var pluginReferences = new List<ResolvedPluginReference>();
      var fileReferences = new List<ResolvedFileReference>();
      var warnings = new List<string>();

      string targetFramework = DetermineTargetFrameworkForPlugin(plugin);

      var nugetDependencies = plugin.GetDependenciesByType(PluginDependencyType.NuGetPackage);
      _logger.LogDebug("Found {Count} NuGet dependencies for {PluginName}.", nugetDependencies.Count, plugin.Metadata.Name);
      foreach (var dependency in nugetDependencies)
      {
        var packageResult = await ResolveNuGetPackageAsync(dependency, targetFramework, cancellationToken);
        if (packageResult.IsSuccess)
        {
          nugetPackages.Add(packageResult.Value);
        }
        else
        {
          var warningMsg = $"Failed to resolve NuGet package '{dependency.Name}@{dependency.Version}': {packageResult.Error.Message}";
          warnings.Add(warningMsg);
          _logger.LogWarning(warningMsg);
        }
      }

      var pluginDependencies = plugin.GetDependenciesByType(PluginDependencyType.Plugin);
      _logger.LogDebug("Found {Count} plugin dependencies for {PluginName}.", pluginDependencies.Count, plugin.Metadata.Name);
      foreach (var dependency in pluginDependencies)
      {
        var pluginResult = await ResolvePluginDependencyAsync(dependency, cancellationToken);
        if (pluginResult.IsSuccess)
        {
          pluginReferences.Add(pluginResult.Value);
        }
        else
        {
          var warningMsg = $"Failed to resolve plugin dependency '{dependency.Name}@{dependency.Version}': {pluginResult.Error.Message}";
          warnings.Add(warningMsg);
          _logger.LogWarning(warningMsg);
        }
      }

      var fileDependencies = plugin.GetDependenciesByType(PluginDependencyType.FileReference);
      _logger.LogDebug("Found {Count} file dependencies for {PluginName}.", fileDependencies.Count, plugin.Metadata.Name);
      foreach (var dependency in fileDependencies)
      {
        var fileResult = await ResolveFileReferenceAsync(dependency, plugin.PluginPath, cancellationToken);
        if (fileResult.IsSuccess)
        {
          fileReferences.Add(fileResult.Value);
        }
        else
        {
          var warningMsg = $"Failed to resolve file reference '{dependency.Name}' (Source: {dependency.Source}): {fileResult.Error.Message}";
          warnings.Add(warningMsg);
          _logger.LogWarning(warningMsg);
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
      _logger.LogError(ex, "Unhandled exception during dependency resolution for plugin: {PluginName}", plugin.Metadata.Name);
      return Result<PluginDependencyContext>.Failure(Error.Failure(
          "PluginDependencyResolver.ResolutionFailed", $"Dependency resolution failed: {ex.Message}"));
    }
  }

  private string DetermineTargetFrameworkForPlugin(Plugin plugin)
  {
    if (plugin.Metadata.Language == PluginLanguage.CSharp)
    {
      return "net8.0";
    }
    return "netstandard2.0";
  }

  public async Task<Result<ResolvedNuGetPackage>> ResolveNuGetPackageAsync(
      PluginDependency dependency,
      string targetFramework = "net8.0",
      CancellationToken cancellationToken = default)
  {
    if (dependency?.Type != PluginDependencyType.NuGetPackage)
      return Result<ResolvedNuGetPackage>.Failure(Error.Validation(
          "PluginDependencyResolver.InvalidDependencyType", "Dependency must be a NuGet package."));

    _logger.LogDebug("Resolving NuGet package: {PackageName} with specifier '{VersionSpecifier}' for TFM '{TFM}'",
        dependency.Name, dependency.Version, targetFramework);

    string sanitizedPackageName = SanitizePathPart(dependency.Name);
    string sanitizedVersionSpecifier = SanitizePathPart(dependency.Version);
    string sanitizedTfm = SanitizePathPart(targetFramework);

    try
    {
      string resolvedSpecificVersion;
      bool isRange = IsVersionSpecifierRange(dependency.Version);

      if (!isRange)
      {
        if (!System.Version.TryParse(dependency.Version, out _))
        {
          _logger.LogWarning("Invalid exact version format for NuGet package {PackageName}: {VersionString}", dependency.Name, dependency.Version);
          return Result<ResolvedNuGetPackage>.Failure(Error.Validation("PluginDependencyResolver.InvalidExactVersion", $"Invalid exact version format: {dependency.Version}"));
        }
        resolvedSpecificVersion = dependency.Version;
      }
      else
      {
        var resolutionResult = await ResolveVersionRangeViaNuGetApiAsync(dependency, targetFramework, cancellationToken);
        if (resolutionResult.IsFailure)
        {
          // No need to create a new error, just propagate the specific failure from resolution
          return Result<ResolvedNuGetPackage>.Failure(resolutionResult.Error);
        }
        resolvedSpecificVersion = resolutionResult.Value;
        _logger.LogInformation("Resolved '{PackageName}@{VersionSpecifier}' to specific version '{ResolvedVersion}' using conceptual API call.",
            dependency.Name, dependency.Version, resolvedSpecificVersion);
      }

      var packageVersionCachePath = Path.Combine(_dependencyCachePath, sanitizedPackageName, sanitizedVersionSpecifier, sanitizedTfm, SanitizePathPart(resolvedSpecificVersion));

      if (Directory.Exists(packageVersionCachePath) && IsPackageCachedIntact(packageVersionCachePath, dependency.Name))
      {
        _logger.LogDebug("Found cached resolved package: {PackageName} v{ResolvedVersion} (spec: {VersionSpecifier}) at {Path}",
            dependency.Name, resolvedSpecificVersion, dependency.Version, packageVersionCachePath);
        return Result<ResolvedNuGetPackage>.Success(LoadResolvedPackageFromCache(dependency, resolvedSpecificVersion, packageVersionCachePath, targetFramework));
      }

      _logger.LogInformation("Package {PackageName} v{ResolvedVersion} (spec: {VersionSpecifier}) not found in cache or incomplete. Attempting download to {Path}.",
          dependency.Name, resolvedSpecificVersion, dependency.Version, packageVersionCachePath);

      var downloadResult = await DownloadAndExtractSpecificNuGetPackageAsync(dependency.Name, resolvedSpecificVersion, targetFramework, packageVersionCachePath, cancellationToken);
      if (downloadResult.IsFailure)
        return Result<ResolvedNuGetPackage>.Failure(downloadResult.Error);

      return Result<ResolvedNuGetPackage>.Success(LoadResolvedPackageFromCache(dependency, resolvedSpecificVersion, packageVersionCachePath, targetFramework));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to resolve NuGet package: {PackageName}@{VersionSpecifier}", dependency.Name, dependency.Version);
      return Result<ResolvedNuGetPackage>.Failure(Error.Failure(
          "PluginDependencyResolver.NuGetResolutionFailed", $"NuGet package resolution for '{dependency.Name}@{dependency.Version}' failed: {ex.Message}"));
    }
  }

  private bool IsVersionSpecifierRange(string versionSpecifier)
  {
    return versionSpecifier.StartsWith("^") ||
           versionSpecifier.StartsWith("~") ||
           versionSpecifier.StartsWith(">") ||
           versionSpecifier.StartsWith("<") ||
           versionSpecifier.Contains("*") ||
           versionSpecifier.Contains("[") || versionSpecifier.Contains("(");
  }

  private string SanitizePathPart(string part)
  {
    if (string.IsNullOrEmpty(part)) return "_empty_";
    char[] invalidChars = Path.GetInvalidFileNameChars().Concat(Path.GetInvalidPathChars()).Distinct().ToArray();
    string regexSearch = new string(invalidChars);
    Regex r = new Regex($"[{Regex.Escape(regexSearch)}]");
    return r.Replace(part, "_");
  }

  private bool IsPackageCachedIntact(string specificVersionPackagePath, string packageName)
  {
    bool nuspecExists = File.Exists(Path.Combine(specificVersionPackagePath, $"{packageName}.nuspec"));
    bool hasLibFolder = Directory.Exists(Path.Combine(specificVersionPackagePath, "lib"));
    // More robust: check for actual DLLs in lib or runtimes if nuspec is missing.
    return nuspecExists || hasLibFolder;
  }

  private async Task<Result<string>> ResolveVersionRangeViaNuGetApiAsync(PluginDependency dependency, string targetFramework, CancellationToken cancellationToken)
  {
    _logger.LogDebug("Conceptual: Querying NuGet API for {PackageName} with specifier {VersionSpecifier} for TFM {TFM}",
        dependency.Name, dependency.Version, targetFramework);
    // ---- PRODUCTION IMPLEMENTATION REQUIRED ----
    // This would use NuGet.Protocol to:
    // 1. Connect to NuGet sources (e.g., nuget.org).
    // 2. Fetch all available versions for `dependency.Name`.
    // 3. Parse `dependency.Version` into a `NuGet.Versioning.VersionRange`.
    // 4. Find the highest stable (or pre-release if allowed by specifier) version that satisfies the range.
    // Example with official libraries (conceptual):
    // try
    // {
    //     ILogger nugetLogger = new NuGetLoggerAdapter(_logger); // You'd need an adapter
    //     var providers = new List<Lazy<INuGetResourceProvider>>();
    //     providers.AddRange(Repository.DefaultProviders);
    //     var packageSource = new NuGet.Configuration.PackageSource("https://api.nuget.org/v3/index.json");
    //     var sourceRepository = new SourceRepository(packageSource, providers);
    //     var metadataResource = await sourceRepository.GetResourceAsync<PackageMetadataResource>(cancellationToken);
    //     var searchFilter = new SearchFilter(includePrerelease: true); // Adjust based on specifier
    //
    //     var versions = new List<NuGetVersion>();
    //     var metadataItems = await metadataResource.GetMetadataAsync(dependency.Name, includePrerelease: true, includeUnlisted: false, new SourceCacheContext(), nugetLogger, cancellationToken);
    //     foreach(var item in metadataItems) versions.Add(item.Identity.Version);
    //
    //     var versionRange = VersionRange.Parse(dependency.Version);
    //     var bestMatch = versionRange.FindBestMatch(versions.Where(v => versionRange.Satisfies(v)));
    //     
    //     if (bestMatch != null) return Result<string>.Success(bestMatch.ToNormalizedString());
    //     return Result<string>.Failure(Error.NotFound("Nuget.NoMatchingVersion", $"No version of {dependency.Name} satisfies '{dependency.Version}'."));
    // }
    // catch (Exception ex)
    // {
    //      _logger.LogError(ex, "Error during NuGet API version resolution for {PackageName}@{VersionSpecifier}", dependency.Name, dependency.Version);
    //      return Result<string>.Failure(Error.Failure("Nuget.ApiError", $"Error resolving version from NuGet API: {ex.Message}"));
    // }
    // ---- END PRODUCTION IMPLEMENTATION REQUIRED ----

    // --- Current Mock Implementation ---
    var versionMatch = Regex.Match(dependency.Version, @"(\d+(\.\d+){0,3})"); // Extracts leading X.Y.Z
    if (versionMatch.Success && System.Version.TryParse(versionMatch.Groups[1].Value, out var parsedVersion))
    {
      string mockResolvedVersion = parsedVersion.ToString();
      _logger.LogWarning("Mocked NuGet range resolution for '{PackageName}@{VersionSpecifier}' to '{MockedVersion}'. Implement actual NuGet API calls.",
          dependency.Name, dependency.Version, mockResolvedVersion);
      return Result<string>.Success(mockResolvedVersion);
    }
    _logger.LogError("Mocked NuGet range resolution FAILED for '{PackageName}@{VersionSpecifier}'. Could not parse a base version.",
        dependency.Name, dependency.Version);
    return Result<string>.Failure(Error.Failure("MockNuget.ResolutionFailed", $"Could not mock-resolve version for {dependency.Name}@{dependency.Version}"));
  }

  public async Task<Result> DownloadAndExtractSpecificNuGetPackageAsync(
      string packageName,
      string specificVersion,
      string targetFramework,
      string packageInstallPath,
      CancellationToken cancellationToken)
  {
    _logger.LogInformation("Downloading NuGet package: {PackageName} v{Version} for TFM {TFM} to {Path}",
        packageName, specificVersion, targetFramework, packageInstallPath);

    var nupkgFileName = $"{packageName.ToLowerInvariant()}.{specificVersion.ToLowerInvariant()}.nupkg";
    var nupkgDownloadUrl = $"https://api.nuget.org/v3-flatcontainer/{packageName.ToLowerInvariant()}/{specificVersion.ToLowerInvariant()}/{nupkgFileName}";

    var tempDownloadDir = Path.Combine(_dependencyCachePath, "_temp_downloads");
    Directory.CreateDirectory(tempDownloadDir);
    var tempNupkgPath = Path.Combine(tempDownloadDir, Guid.NewGuid().ToString("N") + ".nupkg"); // Unique temp file name

    try
    {
      _logger.LogDebug("Downloading from {Url} to temporary path {TempPath}", nupkgDownloadUrl, tempNupkgPath);

      using (var response = await _httpClient.GetAsync(nupkgDownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
      {
        if (!response.IsSuccessStatusCode)
        {
          _logger.LogError("Failed to download NuGet package {PackageName} v{Version} from {Url}. Status: {StatusCode}, Reason: {Reason}",
              packageName, specificVersion, nupkgDownloadUrl, response.StatusCode, response.ReasonPhrase);
          return Result.Failure(Error.Failure("PluginDependencyResolver.DownloadFailedHttpStatus",
              $"Package download for {packageName}@{specificVersion} failed with HTTP status {response.StatusCode}: {response.ReasonPhrase} (URL: {nupkgDownloadUrl})"));
        }
        using (var fileStream = new FileStream(tempNupkgPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
          await response.Content.CopyToAsync(fileStream, cancellationToken);
        }
      }
      _logger.LogDebug("Downloaded {NupkgFileName} successfully to temporary path.", nupkgFileName);

      if (Directory.Exists(packageInstallPath))
      {
        _logger.LogDebug("Clearing existing package install path: {Path}", packageInstallPath);
        Directory.Delete(packageInstallPath, true);
      }
      Directory.CreateDirectory(packageInstallPath);

      _logger.LogDebug("Extracting {NupkgFileName} from {TempPath} to {PackageInstallPath}", nupkgFileName, tempNupkgPath, packageInstallPath);
      ZipFile.ExtractToDirectory(tempNupkgPath, packageInstallPath, true);

      _logger.LogInformation("Successfully downloaded and extracted {PackageName} v{Version} to {PackageInstallPath}",
          packageName, specificVersion, packageInstallPath);

      return Result.Success();
    }
    catch (HttpRequestException ex)
    {
      _logger.LogError(ex, "HttpRequestException while downloading NuGet package {PackageName} v{Version} from {Url}", packageName, specificVersion, nupkgDownloadUrl);
      return Result.Failure(Error.Failure("PluginDependencyResolver.DownloadHttpRequestFailed", $"Package download for {packageName}@{specificVersion} failed: {ex.Message} (URL: {nupkgDownloadUrl})"));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed during download/extraction of NuGet package {PackageName} v{Version}", packageName, specificVersion);
      return Result.Failure(Error.Failure("PluginDependencyResolver.ExtractionFailed", $"Package download or extraction for {packageName}@{specificVersion} failed: {ex.Message}"));
    }
    finally
    {
      if (File.Exists(tempNupkgPath))
      {
        try { File.Delete(tempNupkgPath); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete temporary nupkg: {TempPath}", tempNupkgPath); }
      }
      if (Directory.Exists(tempDownloadDir) && !Directory.EnumerateFileSystemEntries(tempDownloadDir).Any())
      {
        try { Directory.Delete(tempDownloadDir); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete temporary download directory: {TempPath}", tempDownloadDir); }
      }
    }
  }

  private ResolvedNuGetPackage LoadResolvedPackageFromCache(
      PluginDependency originalDependency,
      string resolvedVersion,
      string packageInstallPath,
      string targetFramework)
  {
    var assemblyPaths = new List<string>();
    var runtimeAssemblyPaths = new List<string>();

    var tfmSpecificLibPath = Path.Combine(packageInstallPath, "lib", targetFramework);
    if (Directory.Exists(tfmSpecificLibPath))
    {
      assemblyPaths.AddRange(Directory.GetFiles(tfmSpecificLibPath, "*.dll", SearchOption.TopDirectoryOnly));
    }

    if (!assemblyPaths.Any())
    {
      var commonTfms = new[] { "netstandard2.0", "netstandard2.1" }; // Common fallbacks
      foreach (var tfm in commonTfms)
      {
        var fallbackLibPath = Path.Combine(packageInstallPath, "lib", tfm);
        if (Directory.Exists(fallbackLibPath))
        {
          assemblyPaths.AddRange(Directory.GetFiles(fallbackLibPath, "*.dll", SearchOption.TopDirectoryOnly));
          if (assemblyPaths.Any()) break;
        }
      }
    }
    if (!assemblyPaths.Any())
    {
      var genericLibPath = Path.Combine(packageInstallPath, "lib");
      if (Directory.Exists(genericLibPath))
        assemblyPaths.AddRange(Directory.GetFiles(genericLibPath, "*.dll", SearchOption.AllDirectories)); // More greedy if no TFM match
    }
    if (!assemblyPaths.Any())
    {
      assemblyPaths.AddRange(Directory.GetFiles(packageInstallPath, "*.dll", SearchOption.TopDirectoryOnly));
    }

    var runtimesBasePath = Path.Combine(packageInstallPath, "runtimes");
    if (Directory.Exists(runtimesBasePath))
    {
      foreach (var ridDir in Directory.GetDirectories(runtimesBasePath)) // e.g., runtimes/win-x64
      {
        var nativePath = Path.Combine(ridDir, "native");
        if (Directory.Exists(nativePath))
          runtimeAssemblyPaths.AddRange(Directory.GetFiles(nativePath, "*.dll", SearchOption.AllDirectories));

        var runtimeLibTfmPath = Path.Combine(ridDir, "lib", targetFramework); // e.g., runtimes/win-x64/lib/net8.0
        if (Directory.Exists(runtimeLibTfmPath))
          runtimeAssemblyPaths.AddRange(Directory.GetFiles(runtimeLibTfmPath, "*.dll", SearchOption.AllDirectories));
      }
    }

    if (!assemblyPaths.Any() && !runtimeAssemblyPaths.Any())
    { // Check if any DLLs were found at all
      _logger.LogWarning("No assembly DLLs (ref or runtime) found for {PackageName} v{Version} in expected paths like '{LibPath}'. Plugin might fail.",
          originalDependency.Name, resolvedVersion, tfmSpecificLibPath);
    }

    _logger.LogDebug("Loaded package {PackageName} v{Version} from cache: {Path}. Found {AssemblyCount} ref assemblies, {RuntimeCount} runtime assemblies.",
        originalDependency.Name, resolvedVersion, packageInstallPath, assemblyPaths.Count, runtimeAssemblyPaths.Count);

    return new ResolvedNuGetPackage
    {
      PackageName = originalDependency.Name,
      Version = resolvedVersion,
      AssemblyPaths = assemblyPaths.Distinct().ToList().AsReadOnly(),
      RuntimeDependencies = runtimeAssemblyPaths.Distinct().ToList().AsReadOnly(),
      InstallationPath = packageInstallPath,
      TargetFramework = targetFramework,
      Metadata = new Dictionary<string, string>
      {
        ["OriginalSpecifier"] = originalDependency.Version,
        ["ResolvedAt"] = DateTimeOffset.UtcNow.ToString("o")
      }
    };
  }

  public async Task<Result<PluginDependencyValidation>> ValidateDependenciesAsync(
      Plugin plugin,
      CancellationToken cancellationToken = default)
  {
    if (plugin is null)
      return Result<PluginDependencyValidation>.Failure(Error.Validation(
          "PluginDependencyResolver.PluginNullValidation", "Plugin cannot be null for validation."));

    _logger.LogInformation("Validating dependencies for plugin: {PluginName} v{PluginVersion}",
        plugin.Metadata.Name, plugin.Metadata.Version);

    var issues = new List<DependencyValidationIssue>();
    var totalDependencies = plugin.Dependencies.Count;
    var resolvedCount = 0;
    string targetFramework = DetermineTargetFrameworkForPlugin(plugin);

    foreach (var dependency in plugin.Dependencies)
    {
      var validationResult = await ValidateSingleDependencyAsync(dependency, plugin.PluginPath, targetFramework, cancellationToken);
      if (validationResult.IsSuccess && validationResult.Value)
      {
        resolvedCount++;
      }
      else
      {
        string message = validationResult.IsSuccess ?
                         $"Dependency '{dependency.GetDescription()}' could not be satisfied or found." :
                         validationResult.Error.Message; // Use the error message from validationResult if it failed

        Error originatingError = validationResult.IsFailure ? validationResult.Error : Error.Validation("Dependency.Unsatisfied", message);

        issues.Add(new DependencyValidationIssue
        {
          Severity = DependencyIssueSeverity.Error,
          Dependency = dependency,
          Message = message, // Use the more specific message
          SuggestedResolution = GetSuggestedResolution(dependency)
        });
      }
    }
    var finalValidationResult = !issues.Any()
        ? PluginDependencyValidation.Success(totalDependencies)
        : PluginDependencyValidation.Failed(issues, totalDependencies, resolvedCount);

    _logger.LogInformation("Dependency validation completed for plugin: {PluginName}. Valid: {IsValid}, Total: {Total}, Resolved: {Resolved}, Issues: {IssueCount}",
        plugin.Metadata.Name, finalValidationResult.IsValid, totalDependencies, resolvedCount, issues.Count);

    return Result<PluginDependencyValidation>.Success(finalValidationResult);
  }

  private async Task<Result<bool>> ValidateSingleDependencyAsync(
      PluginDependency dependency,
      string basePluginDirectory,
      string targetFramework,
      CancellationToken cancellationToken)
  {
    _logger.LogTrace("Validating single dependency: {DependencyDescription}", dependency.GetDescription());
    switch (dependency.Type)
    {
      case PluginDependencyType.NuGetPackage:
        return await ValidateNuGetPackageAsync(dependency, targetFramework, cancellationToken);
      case PluginDependencyType.Plugin:
        return await ValidatePluginReferenceAsync(dependency, cancellationToken); // Renamed for clarity
      case PluginDependencyType.FileReference:
        return StaticValidateFileReference(dependency, basePluginDirectory); // Can be static
      default:
        _logger.LogWarning("Unsupported dependency type for validation: {DependencyType}", dependency.Type);
        return Result<bool>.Failure(Error.Validation(
            "PluginDependencyResolver.UnsupportedValidationType", $"Unsupported dependency type for validation: {dependency.Type}"));
    }
  }

  private async Task<Result<bool>> ValidateNuGetPackageAsync(PluginDependency dependency, string targetFramework, CancellationToken cancellationToken)
  {
    _logger.LogTrace("Attempting conceptual resolution for NuGet validation: {PackageName}@{VersionSpecifier} for TFM {TFM}",
        dependency.Name, dependency.Version, targetFramework);
    var resolutionAttempt = await ResolveNuGetPackageAsync(dependency, targetFramework, cancellationToken);

    if (resolutionAttempt.IsSuccess)
    {
      _logger.LogDebug("NuGet dependency {PackageName}@{VersionSpecifier} validated successfully (resolved to {ResolvedVersion}).",
          dependency.Name, dependency.Version, resolutionAttempt.Value.Version);
      return Result<bool>.Success(true);
    }

    _logger.LogWarning("NuGet dependency {PackageName}@{VersionSpecifier} failed validation: {Error}",
        dependency.Name, dependency.Version, resolutionAttempt.Error.Message);
    // Create a new error with a more specific code/message for validation failure
    return Result<bool>.Failure(Error.Validation(
        $"{resolutionAttempt.Error.Code}.ValidationFailed", // Prepend original code
        $"NuGet package validation failed for {dependency.Name}@{dependency.Version}: {resolutionAttempt.Error.Message}"
    ));
  }

  // Renamed from ValidatePluginDependencyAsync for clarity as this is for reference validation
  private async Task<Result<bool>> ValidatePluginReferenceAsync(PluginDependency dependency, CancellationToken cancellationToken)
  {
    _logger.LogTrace("Validating plugin reference: {PluginName}@{VersionSpecifier}", dependency.Name, dependency.Version);
    var resolutionResult = await ResolvePluginDependencyAsync(dependency, cancellationToken);

    if (resolutionResult.IsSuccess)
    {
      if (resolutionResult.Value.IsAvailable)
      {
        _logger.LogDebug("Plugin reference {PluginName}@{VersionSpecifier} is valid and available (resolved to {ResolvedVersion}).",
           dependency.Name, dependency.Version, resolutionResult.Value.Version);
        return Result<bool>.Success(true);
      }
      else
      {
        _logger.LogWarning("Plugin reference {PluginName}@{VersionSpecifier} resolved to {ResolvedVersion}, but it's not available (Status: Error/Disabled).",
            dependency.Name, dependency.Version, resolutionResult.Value.Version);
        return Result<bool>.Failure(Error.Validation("PluginDependency.NotAvailable", $"Referenced plugin {dependency.Name} (v{resolutionResult.Value.Version}) is not available."));
      }
    }
    _logger.LogWarning("Plugin reference {PluginName}@{VersionSpecifier} could not be resolved/validated: {Error}",
        dependency.Name, dependency.Version, resolutionResult.Error.Message);
    return Result<bool>.Failure(Error.Validation(
        $"{resolutionResult.Error.Code}.ValidationFailed",
        $"Plugin reference validation failed for {dependency.Name}@{dependency.Version}: {resolutionResult.Error.Message}"
    ));
  }

  // Made this static as it doesn't depend on instance state
  private static Result<bool> StaticValidateFileReference(PluginDependency dependency, string basePluginDirectory)
  {
    if (string.IsNullOrWhiteSpace(dependency.Source))
      return Result<bool>.Failure(Error.Validation(
          "FileReference.SourcePathEmpty", "File reference 'source' (path) cannot be empty."));

    string fullPath = dependency.Source;
    if (!Path.IsPathRooted(fullPath))
    {
      fullPath = Path.Combine(basePluginDirectory, fullPath);
    }
    fullPath = Path.GetFullPath(fullPath);

    if (File.Exists(fullPath))
    {
      return Result<bool>.Success(true);
    }
    return Result<bool>.Failure(Error.NotFound("FileReference.NotFound", $"File reference '{dependency.Name}' (path: {fullPath}) not found."));
  }

  public async Task<Result<ResolvedPluginReference>> ResolvePluginDependencyAsync(
       PluginDependency dependency,
       CancellationToken cancellationToken = default)
  {
    // This method's implementation from the previous turn was mostly correct.
    // Just ensuring it exists with the correct signature.
    if (dependency?.Type != PluginDependencyType.Plugin)
      return Result<ResolvedPluginReference>.Failure(Error.Validation(
          "PluginDependencyResolver.InvalidDepTypeForPluginRef", "Dependency must be a plugin reference."));

    _logger.LogDebug("Resolving plugin reference: {PluginName} specifier '{VersionSpecifier}'", dependency.Name, dependency.Version);

    try
    {
      var plugins = await _pluginRepository.GetAllAsync(cancellationToken);
      var matchingPlugin = plugins
          .Where(p => p.Metadata.Name.Equals(dependency.Name, StringComparison.OrdinalIgnoreCase))
          .Where(p => dependency.IsVersionSatisfied(p.Metadata.Version.ToString()))
          .OrderByDescending(p => p.Metadata.Version)
          .FirstOrDefault();

      if (matchingPlugin is null)
      {
        _logger.LogWarning("Plugin reference {PluginName} satisfying '{VersionSpecifier}' not found.", dependency.Name, dependency.Version);
        return Result<ResolvedPluginReference>.Failure(Error.NotFound(
            "PluginResolver.ReferencedPluginNotFound",
            $"Referenced plugin '{dependency.Name}' satisfying version specifier '{dependency.Version}' not found."));
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

      _logger.LogDebug("Resolved plugin reference: {PluginName} specifier '{VersionSpecifier}' -> {ResolvedPlugin} v{ResolvedVersion} (Available: {IsAvailable})",
          dependency.Name, dependency.Version, reference.PluginName, reference.Version, reference.IsAvailable);

      return Result<ResolvedPluginReference>.Success(reference);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to resolve plugin reference: {PluginName}@{VersionSpecifier}", dependency.Name, dependency.Version);
      return Result<ResolvedPluginReference>.Failure(Error.Failure(
          "PluginResolver.PluginReferenceResolutionFailed", $"Plugin reference resolution for '{dependency.Name}' failed: {ex.Message}"));
    }
  }

  public async Task<Result<ResolvedFileReference>> ResolveFileReferenceAsync(
      PluginDependency dependency,
      string baseDirectory,
      CancellationToken cancellationToken = default)
  {
    // This method's implementation from the previous turn was mostly correct.
    if (dependency?.Type != PluginDependencyType.FileReference)
      return Result<ResolvedFileReference>.Failure(Error.Validation(
          "PluginDependencyResolver.InvalidDepTypeForFileRef", "Dependency must be a file reference."));

    _logger.LogDebug("Resolving file reference: Name '{FileName}', Source Path '{SourcePath}', Version '{VersionSpecifier}' relative to '{BaseDirectory}'",
        dependency.Name, dependency.Source, dependency.Version, baseDirectory);
    try
    {
      var filePath = dependency.Source;
      if (string.IsNullOrWhiteSpace(filePath))
      {
        return Result<ResolvedFileReference>.Failure(Error.Validation(
            "FileReference.SourcePathEmpty", "File reference 'source' (path) attribute cannot be empty."));
      }

      if (!Path.IsPathRooted(filePath))
      {
        filePath = Path.Combine(baseDirectory, filePath);
      }
      filePath = Path.GetFullPath(filePath);

      var fileExists = File.Exists(filePath);
      FileInfo? fileInfo = fileExists ? new FileInfo(filePath) : null;

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
        _logger.LogWarning("File reference does not exist: {FilePath} (Logical Name: {LogicalName})", filePath, dependency.Name);
        return Result<ResolvedFileReference>.Failure(Error.NotFound(
           "FileReference.NotFoundOnResolve", $"File for reference '{dependency.Name}' not found at path '{filePath}'."));
      }
      _logger.LogDebug("Resolved file reference '{LogicalName}' to actual path '{FilePath}'", dependency.Name, filePath);
      return Result<ResolvedFileReference>.Success(reference);
    }
    catch (ArgumentException ex)
    {
      _logger.LogError(ex, "Invalid path for file reference: {FileName} (Source: {SourcePath})", dependency.Name, dependency.Source);
      return Result<ResolvedFileReference>.Failure(Error.Validation(
          "FileReference.InvalidPath", $"Invalid file path for '{dependency.Name}': {ex.Message}"));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to resolve file reference: {FileName} (Source: {SourcePath})", dependency.Name, dependency.Source);
      return Result<ResolvedFileReference>.Failure(Error.Failure(
          "FileReference.ResolutionFailed", $"File reference resolution for '{dependency.Name}' failed: {ex.Message}"));
    }
  }

  // This method can remain static.
  private static string GetSuggestedResolution(PluginDependency dependency)
  {
    return dependency.Type switch
    {
      PluginDependencyType.NuGetPackage => $"Ensure NuGet package '{dependency.Name}' version '{dependency.Version}' is valid and available from configured sources.",
      PluginDependencyType.Plugin => $"Ensure plugin '{dependency.Name}' satisfying version '{dependency.Version}' is registered and available.",
      PluginDependencyType.FileReference => $"Ensure file '{dependency.Name}' (path: '{dependency.Source}') exists relative to its plugin or as an absolute path.",
      _ => "Check dependency configuration in plugin.json."
    };
  }

  public async Task ClearDependencyCacheAsync(PluginId? pluginId = null, CancellationToken cancellationToken = default)
  {
    // Corrected to use 'pluginId != null'
    if (pluginId != null)
    {
      _logger.LogWarning("Plugin-specific cache clearing for PluginId {PluginIdValue} is targeted but current implementation clears globally or more broadly based on package name. For true plugin-specific isolation, cache structure would need PluginId as a top-level dir.", pluginId.Value);
      // Example for specific plugin (if cache was structured by PluginId first):
      // string pluginSpecificCacheDir = Path.Combine(_dependencyCachePath, pluginId.Value.ToString());
      // if(Directory.Exists(pluginSpecificCacheDir)) delete it.
      // For now, this will proceed to the global-like clear.
    }

    _logger.LogInformation("Attempting to clear dependency cache at {CachePath}", _dependencyCachePath);
    try
    {
      if (Directory.Exists(_dependencyCachePath))
      {
        var directoryInfo = new DirectoryInfo(_dependencyCachePath);
        // Be careful with deleting everything in a shared temp path.
        // This deletes subfolders named like packages, and the _temp_downloads folder.
        foreach (DirectoryInfo dir in directoryInfo.GetDirectories())
        {
          try
          {
            dir.Delete(true);
            _logger.LogDebug("Deleted cached directory: {Directory}", dir.FullName);
          }
          catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete cached directory: {Directory}", dir.FullName); }
        }
        foreach (FileInfo file in directoryInfo.GetFiles()) // Delete loose files too
        {
          try { file.Delete(); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete cached file: {File}", file.FullName); }
        }
        _logger.LogInformation("Dependency cache cleared successfully from: {CachePath}", _dependencyCachePath);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to clear dependency cache path: {CachePath}", _dependencyCachePath);
    }
    await Task.CompletedTask;
  }

  public async Task<Result<PluginDependencyGraph>> GetDependencyGraphAsync(
      Plugin plugin,
      bool includeTransitive = true,
      CancellationToken cancellationToken = default)
  {
    if (plugin is null)
      return Result<PluginDependencyGraph>.Failure(Error.Validation(
          "PluginDependencyResolver.PluginNullGraph", "Plugin cannot be null for graph generation."));

    _logger.LogInformation("Building dependency graph for plugin: {PluginName} v{Version} (Include Transitive: {IncludeTransitive})",
        plugin.Metadata.Name, plugin.Metadata.Version, includeTransitive);

    var visitedNodesInCurrentPath = new HashSet<string>();
    var allResolvedNodes = new Dictionary<string, DependencyGraphNode>();
    var directDependenciesAsNodes = new List<DependencyGraphNode>();
    var circularWarnings = new List<string>();
    int maxDepth = 0;

    string pluginTfm = DetermineTargetFrameworkForPlugin(plugin);

    async Task<DependencyGraphNode> BuildNodeRecursiveAsync(PluginDependency currentDep, int currentDepth)
    {
      maxDepth = Math.Max(maxDepth, currentDepth);
      string nodeKey = $"{currentDep.Type}:{currentDep.Name}@{currentDep.Version}";

      if (allResolvedNodes.TryGetValue(nodeKey, out var existingNode))
      {
        return existingNode;
      }

      if (visitedNodesInCurrentPath.Contains(nodeKey))
      {
        var circularMsg = $"Circular dependency detected: '{nodeKey}' encountered again in current path at depth {currentDepth}.";
        if (!circularWarnings.Contains(circularMsg)) circularWarnings.Add(circularMsg);
        _logger.LogWarning(circularMsg);
        var cycleNode = new DependencyGraphNode { Dependency = currentDep, Depth = currentDepth, IsResolved = false, ErrorMessage = "Circular dependency" };
        allResolvedNodes[nodeKey] = cycleNode;
        return cycleNode;
      }
      visitedNodesInCurrentPath.Add(nodeKey);

      var childrenNodes = new List<DependencyGraphNode>();
      // Use the specific target framework of the current plugin context for validating its dependencies
      var validationResult = await ValidateSingleDependencyAsync(currentDep, plugin.PluginPath, pluginTfm, cancellationToken);
      bool isCurrentResolved = validationResult.IsSuccess && validationResult.Value;
      string? currentErrorMessage = !isCurrentResolved ? (validationResult.IsFailure ? validationResult.Error.Message : $"Dependency '{currentDep.GetDescription()}' not satisfied.") : null;

      if (isCurrentResolved && includeTransitive && currentDep.Type == PluginDependencyType.Plugin)
      {
        var pluginRefResult = await ResolvePluginDependencyAsync(currentDep, cancellationToken);
        if (pluginRefResult.IsSuccess)
        {
          var referencedPlugin = await _pluginRepository.GetByIdAsync(pluginRefResult.Value.PluginId, cancellationToken);
          if (referencedPlugin != null && referencedPlugin.Status == PluginStatus.Available)
          {
            foreach (var childDep in referencedPlugin.Dependencies)
            {
              childrenNodes.Add(await BuildNodeRecursiveAsync(childDep, currentDepth + 1));
            }
          }
          else if (referencedPlugin == null)
          {
            currentErrorMessage = (currentErrorMessage == null ? "" : currentErrorMessage + " ") + $"Referenced plugin entity for '{currentDep.Name}' not found in repository.";
            isCurrentResolved = false;
          }
          else
          {
            currentErrorMessage = (currentErrorMessage == null ? "" : currentErrorMessage + " ") + $"Referenced plugin '{currentDep.Name}' is not available (Status: {referencedPlugin.Status}).";
            isCurrentResolved = false;
          }
        }
        else
        {
          currentErrorMessage = (currentErrorMessage == null ? "" : currentErrorMessage + " ") + $"Could not resolve plugin reference: {pluginRefResult.Error.Message}";
          isCurrentResolved = false;
        }
      }

      visitedNodesInCurrentPath.Remove(nodeKey);

      var newNode = new DependencyGraphNode
      {
        Dependency = currentDep,
        Depth = currentDepth,
        Children = childrenNodes.AsReadOnly(),
        IsResolved = isCurrentResolved,
        ErrorMessage = currentErrorMessage
      };
      allResolvedNodes[nodeKey] = newNode;
      return newNode;
    }

    foreach (var dependency in plugin.Dependencies)
    {
      directDependenciesAsNodes.Add(await BuildNodeRecursiveAsync(dependency, 0));
    }

    var finalAllNodesList = allResolvedNodes.Values.OrderBy(n => n.Depth).ThenBy(n => n.Dependency.Name).ToList();

    var graph = new PluginDependencyGraph
    {
      RootPluginId = plugin.Id,
      DirectDependencies = directDependenciesAsNodes.AsReadOnly(),
      AllDependencies = finalAllNodesList.AsReadOnly(),
      CircularDependencyWarnings = circularWarnings.AsReadOnly(),
      MaxDepth = maxDepth
    };

    _logger.LogInformation("Built dependency graph for plugin {PluginName}. Direct: {DirectCount}, Total Unique Nodes: {AllCount}, Max Depth: {MaxDepth}, Circular Warnings: {WarningCount}",
        plugin.Metadata.Name, directDependenciesAsNodes.Count, finalAllNodesList.Count, maxDepth, graph.CircularDependencyWarnings.Count);

    return Result<PluginDependencyGraph>.Success(graph);
  }
}