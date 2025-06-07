using DevFlow.Application.Plugins.Runtime;
using DevFlow.Application.Plugins.Runtime.Models;
using DevFlow.Domain.Plugins.Entities;
using DevFlow.Domain.Plugins.Enums;
using DevFlow.SharedKernel.Results;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DevFlow.Infrastructure.Plugins.Runtime;

/// <summary>
/// Enhanced C# runtime manager with improved error handling and debugging capabilities.
/// </summary>
public sealed class CSharpRuntimeManager : IPluginRuntimeManager, IDisposable
{
  private readonly ILogger<CSharpRuntimeManager> _logger;
  private readonly IPluginDependencyResolver _dependencyResolver;
  private readonly string _compilationCachePath;
  private readonly string _buildOutputPath;
  private readonly ConcurrentDictionary<string, CompiledPluginInfo> _compilationCache;
  private readonly ConcurrentDictionary<string, WeakReference<AssemblyLoadContext>> _loadContexts;
  private readonly object _compilationLock = new();
  private bool _disposed;

  public CSharpRuntimeManager(
      ILogger<CSharpRuntimeManager> logger,
      IPluginDependencyResolver dependencyResolver)
  {
    _logger = logger;
    _dependencyResolver = dependencyResolver;

    // Initialize cache directories
    _compilationCachePath = Path.Combine(Path.GetTempPath(), "DevFlow", "CompiledPlugins");
    _buildOutputPath = Path.Combine(Path.GetTempPath(), "DevFlowBuilds");

    Directory.CreateDirectory(_compilationCachePath);
    Directory.CreateDirectory(_buildOutputPath);

    _compilationCache = new ConcurrentDictionary<string, CompiledPluginInfo>();
    _loadContexts = new ConcurrentDictionary<string, WeakReference<AssemblyLoadContext>>();

    _logger.LogInformation("Enhanced C# Runtime Manager initialized. Cache: {CachePath}, Builds: {BuildPath}",
        _compilationCachePath, _buildOutputPath);
  }

  public IReadOnlyList<PluginLanguage> SupportedLanguages { get; } = new[] { PluginLanguage.CSharp };
  public string RuntimeId => "enhanced-csharp-runtime";



  #region INSERT
  public async Task<Result<PluginExecutionResult>> ExecuteAsync(
      Plugin plugin,
      PluginExecutionContext context,
      CancellationToken cancellationToken = default)
  {
    if (plugin is null)
      return Result<PluginExecutionResult>.Failure(Error.Validation(
          "CSharpRuntime.PluginNull", "Plugin cannot be null."));

    if (!CanExecutePlugin(plugin))
      return Result<PluginExecutionResult>.Failure(Error.Validation(
          "CSharpRuntime.UnsupportedPlugin", $"Plugin language '{plugin.Metadata.Language}' is not supported."));

    var startTime = DateTimeOffset.UtcNow;
    var logs = new List<string>();
    var stopwatch = Stopwatch.StartNew();

    try
    {
      _logger.LogDebug("Starting execution of C# plugin: {PluginName}", plugin.Metadata.Name);
      logs.Add($"Starting C# plugin execution: {plugin.Metadata.Name} v{plugin.Metadata.Version}");

      // Get or compile the plugin
      var compilationResult = await GetOrCompilePluginAsync(plugin, logs, cancellationToken);
      if (compilationResult.IsFailure)
        return CreateFailureResult(compilationResult.Error, startTime, logs);

      var compiledInfo = compilationResult.Value;
      logs.Add("Plugin compilation/cache retrieval successful");

      // Create isolated execution context
      var executionResult = await ExecuteInIsolatedContextAsync(compiledInfo, context, logs, cancellationToken);

      stopwatch.Stop();
      var endTime = DateTimeOffset.UtcNow;

      if (executionResult.IsFailure)
      {
        logs.Add($"Plugin execution failed: {executionResult.Error.Message}");
        return Result<PluginExecutionResult>.Success(PluginExecutionResult.Failure(
            executionResult.Error, startTime, endTime, logs, null, GetCurrentMemoryUsage()));
      }

      logs.Add($"Plugin execution completed successfully in {stopwatch.ElapsedMilliseconds}ms");
      _logger.LogDebug("C# plugin execution completed: {PluginName} in {Duration}ms",
          plugin.Metadata.Name, stopwatch.ElapsedMilliseconds);

      return Result<PluginExecutionResult>.Success(PluginExecutionResult.Success(
          executionResult.Value, startTime, endTime, logs, GetCurrentMemoryUsage()));
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      return CreateCancelledResult(startTime, logs);
    }
    catch (Exception ex)
    {
      return CreateExceptionResult(ex, startTime, logs);
    }
  }

  private async Task<Result<object?>> ExecuteInIsolatedContextAsync(
      CompiledPluginInfo compiledInfo,
      PluginExecutionContext context,
      List<string> logs,
      CancellationToken cancellationToken)
  {
    var contextKey = $"{compiledInfo.CacheKey}_{DateTime.UtcNow.Ticks}";

    try
    {
      // Create isolated assembly load context
      var loadContext = new PluginAssemblyLoadContext(contextKey, compiledInfo.Dependencies);
      _loadContexts[contextKey] = new WeakReference<AssemblyLoadContext>(loadContext);

      // Load the plugin assembly
      var assembly = loadContext.LoadFromAssemblyPath(compiledInfo.AssemblyPath);
      logs.Add("Plugin assembly loaded in isolated context");

      // Find and instantiate plugin class
      var pluginInstanceResult = CreatePluginInstance(assembly, logs);
      if (pluginInstanceResult.IsFailure)
        return Result<object?>.Failure(pluginInstanceResult.Error);

      var pluginInstance = pluginInstanceResult.Value;
      logs.Add("Plugin instance created");

      // Execute with timeout
      using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
      timeoutCts.CancelAfter(context.ExecutionTimeout);

      var executionResult = await ExecutePluginMethodAsync(pluginInstance, context, logs, timeoutCts.Token);

      return executionResult;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to execute plugin in isolated context");
      return Result<object?>.Failure(Error.Failure(
          "CSharpRuntime.IsolatedExecutionFailed", $"Isolated execution failed: {ex.Message}"));
    }
    finally
    {
      // Cleanup load context
      if (_loadContexts.TryRemove(contextKey, out var contextRef))
      {
        if (contextRef.TryGetTarget(out var loadContext))
        {
          try
          {
            loadContext.Unload();
            logs.Add("Plugin execution context unloaded");
          }
          catch (Exception ex)
          {
            _logger.LogWarning(ex, "Failed to unload plugin context: {ContextKey}", contextKey);
          }
        }
      }
    }
  }

  private Result<object> CreatePluginInstance(Assembly assembly, List<string> logs)
  {
    try
    {
      var pluginTypes = assembly.GetTypes()
          .Where(t => t.IsClass && !t.IsAbstract)
          .Where(t => HasPluginInterface(t) || HasPluginAttribute(t) || t.Name.Contains("Plugin", StringComparison.OrdinalIgnoreCase))
          .ToList();

      if (!pluginTypes.Any())
      {
        return Result<object>.Failure(Error.Validation(
            "CSharpRuntime.NoPluginClass", "No plugin class found."));
      }

      var pluginType = pluginTypes.First();
      logs.Add($"Found plugin class: {pluginType.FullName}");

      var instance = Activator.CreateInstance(pluginType);
      if (instance is null)
      {
        return Result<object>.Failure(Error.Failure(
            "CSharpRuntime.InstanceCreationFailed", "Failed to create plugin instance."));
      }

      return Result<object>.Success(instance);
    }
    catch (Exception ex)
    {
      return Result<object>.Failure(Error.Failure(
          "CSharpRuntime.InstanceException", $"Plugin instance creation failed: {ex.Message}"));
    }
  }

  private async Task<Result<object?>> ExecutePluginMethodAsync(
      object pluginInstance,
      PluginExecutionContext context,
      List<string> logs,
      CancellationToken cancellationToken)
  {
    try
    {
      var executeMethod = pluginInstance.GetType().GetMethod("Execute") ??
                         pluginInstance.GetType().GetMethod("ExecuteAsync");

      if (executeMethod is null)
      {
        return Result<object?>.Failure(Error.Validation(
            "CSharpRuntime.NoExecuteMethod", "Plugin class must have an Execute or ExecuteAsync method."));
      }

      logs.Add($"Invoking method: {executeMethod.Name}");

      var dynamicContext = CreateDynamicContext(context);

      object? result;
      if (executeMethod.Name == "ExecuteAsync")
      {
        var task = (Task)executeMethod.Invoke(pluginInstance, new object[] { dynamicContext, cancellationToken })!;
        await task;

        if (executeMethod.ReturnType.IsGenericType)
        {
          var resultProperty = task.GetType().GetProperty("Result");
          result = resultProperty?.GetValue(task);
        }
        else
        {
          result = null;
        }
      }
      else
      {
        result = executeMethod.Invoke(pluginInstance, new object[] { dynamicContext });
      }

      logs.Add("Plugin method execution completed");
      return Result<object?>.Success(result);
    }
    catch (Exception ex)
    {
      return Result<object?>.Failure(Error.Failure(
          "CSharpRuntime.ExecutionException", $"Plugin execution failed: {ex.Message}"));
    }
  }

  // Helper methods
  private static bool HasPluginInterface(Type type)
  {
    var interfaces = type.GetInterfaces();
    return interfaces.Any(i => i.Name.Contains("Plugin", StringComparison.OrdinalIgnoreCase));
  }

  private static bool HasPluginAttribute(Type type)
  {
    var attributes = type.GetCustomAttributes(true);
    return attributes.Any(a => a.GetType().Name.Contains("Plugin", StringComparison.OrdinalIgnoreCase));
  }

  private static long GetCurrentMemoryUsage()
  {
    return GC.GetTotalMemory(false);
  }

  private static object CreateDynamicContext(PluginExecutionContext context)
  {
    return new
    {
      Configuration = context.ExecutionParameters,
      InputData = context.InputData,
      WorkingDirectory = context.WorkingDirectory,
      EnvironmentVariables = context.EnvironmentVariables,
      CorrelationId = context.CorrelationId,
      Timeout = context.ExecutionTimeout,
      MaxMemoryBytes = context.MaxMemoryBytes
    };
  }

  // Result creation helpers
  private Result<PluginExecutionResult> CreateFailureResult(Error error, DateTimeOffset startTime, List<string> logs)
  {
    return Result<PluginExecutionResult>.Success(PluginExecutionResult.Failure(
        error, startTime, DateTimeOffset.UtcNow, logs, null, GetCurrentMemoryUsage()));
  }

  private Result<PluginExecutionResult> CreateCancelledResult(DateTimeOffset startTime, List<string> logs)
  {
    logs.Add("Plugin execution was cancelled");
    var error = Error.Failure("CSharpRuntime.ExecutionCancelled", "Plugin execution was cancelled.");
    return Result<PluginExecutionResult>.Success(PluginExecutionResult.Failure(
        error, startTime, DateTimeOffset.UtcNow, logs, null, GetCurrentMemoryUsage()));
  }

  private Result<PluginExecutionResult> CreateExceptionResult(Exception ex, DateTimeOffset startTime, List<string> logs)
  {
    logs.Add($"Unhandled exception during plugin execution: {ex.Message}");
    _logger.LogError(ex, "Unhandled exception during C# plugin execution");
    return Result<PluginExecutionResult>.Success(PluginExecutionResult.Failure(
        ex, startTime, DateTimeOffset.UtcNow, logs));
  }
  #endregion INSERT
  

  public async Task<Result<bool>> ValidatePluginAsync(
      Plugin plugin,
      CancellationToken cancellationToken = default)
  {
    if (plugin is null)
      return Result<bool>.Failure(Error.Validation(
          "CSharpRuntime.PluginNull", "Plugin cannot be null."));

    if (!CanExecutePlugin(plugin))
      return Result<bool>.Failure(Error.Validation(
          "CSharpRuntime.UnsupportedPlugin", $"Plugin language '{plugin.Metadata.Language}' is not supported."));

    try
    {
      _logger.LogDebug("Validating C# plugin: {PluginName}", plugin.Metadata.Name);

      // Basic file system validation
      var entryPointPath = Path.Combine(plugin.PluginPath, plugin.EntryPoint);
      if (!File.Exists(entryPointPath))
      {
        _logger.LogWarning("Entry point file not found: {EntryPointPath}", entryPointPath);
        return Result<bool>.Failure(Error.Validation(
            "CSharpRuntime.EntryPointNotFound", $"Entry point file '{entryPointPath}' does not exist."));
      }

      if (!entryPointPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
      {
        return Result<bool>.Failure(Error.Validation(
            "CSharpRuntime.InvalidEntryPoint", "C# plugin entry point must be a .cs file."));
      }

      // Validate dependencies can be resolved
      var dependencyResult = await _dependencyResolver.ValidateDependenciesAsync(plugin, cancellationToken);
      if (dependencyResult.IsFailure)
      {
        _logger.LogWarning("Dependency validation failed for {PluginName}: {Error}",
            plugin.Metadata.Name, dependencyResult.Error.Message);
        return Result<bool>.Failure(dependencyResult.Error);
      }

      var validation = dependencyResult.Value;
      if (!validation.IsValid)
      {
        var issues = string.Join("; ", validation.Issues.Select(i => i.Message));
        _logger.LogWarning("Plugin has dependency issues: {PluginName}: {Issues}",
            plugin.Metadata.Name, issues);
        return Result<bool>.Failure(Error.Validation(
            "CSharpRuntime.DependencyIssues", $"Plugin has dependency issues: {issues}"));
      }

      // Try compilation to validate syntax and dependencies
      var logs = new List<string>();
      var compilationResult = await GetOrCompilePluginAsync(plugin, logs, cancellationToken);
      if (compilationResult.IsFailure)
      {
        _logger.LogWarning("Compilation validation failed for {PluginName}: {Error}. Detailed logs: {Logs}",
            plugin.Metadata.Name, compilationResult.Error.Message, string.Join("\n", logs));
        return Result<bool>.Failure(compilationResult.Error);
      }

      _logger.LogDebug("Plugin validation successful: {PluginName}", plugin.Metadata.Name);
      return Result<bool>.Success(true);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to validate C# plugin: {PluginName}", plugin.Metadata.Name);
      return Result<bool>.Failure(Error.Failure(
          "CSharpRuntime.ValidationFailed", $"Plugin validation failed: {ex.Message}"));
    }
  }

  public bool CanExecutePlugin(Plugin plugin)
  {
    return plugin?.Metadata?.Language == PluginLanguage.CSharp;
  }

  public Task<Result> InitializeAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      _logger.LogInformation("Initializing Enhanced C# runtime manager");

      // Verify .NET SDK availability
      var dotnetPath = FindDotNetExecutable();
      if (string.IsNullOrEmpty(dotnetPath))
      {
        _logger.LogError(".NET CLI not found in PATH. C# plugin compilation will not work.");
        return Task.FromResult(Result.Failure(Error.Failure(
            "CSharpRuntime.DotNetNotFound", ".NET CLI not found in PATH.")));
      }

      _logger.LogInformation("Found .NET CLI at: {DotNetPath}", dotnetPath);

      // Test .NET CLI functionality
      var testResult = TestDotNetCli(dotnetPath);
      if (testResult.IsFailure)
      {
        return Task.FromResult(testResult);
      }

      // Clean up old build artifacts
      CleanupOldBuildArtifacts();

      _logger.LogInformation("Enhanced C# runtime manager initialized successfully");
      return Task.FromResult(Result.Success());
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to initialize Enhanced C# runtime manager");
      return Task.FromResult(Result.Failure(Error.Failure(
          "CSharpRuntime.InitializationFailed", $"Runtime initialization failed: {ex.Message}")));
    }
  }

  public Task DisposeAsync(CancellationToken cancellationToken = default)
  {
    Dispose();
    return Task.CompletedTask;
  }

  // Enhanced compilation methods with better error handling

  private async Task<Result<CompiledPluginInfo>> GetOrCompilePluginAsync(
      Plugin plugin,
      List<string> logs,
      CancellationToken cancellationToken)
  {
    var cacheKey = GeneratePluginCacheKey(plugin);

    // Check if already compiled and cached
    if (_compilationCache.TryGetValue(cacheKey, out var cachedInfo))
    {
      if (File.Exists(cachedInfo.AssemblyPath))
      {
        logs.Add($"Using cached compilation: {cachedInfo.AssemblyPath}");
        _logger.LogDebug("Using cached compilation for {PluginName}: {AssemblyPath}",
            plugin.Metadata.Name, cachedInfo.AssemblyPath);
        return Result<CompiledPluginInfo>.Success(cachedInfo);
      }
      else
      {
        _compilationCache.TryRemove(cacheKey, out _);
        logs.Add("Cached assembly file missing, recompiling...");
      }
    }

    return await Task.Run(() =>
    {
      lock (_compilationLock)
      {
        // Double-check after acquiring lock
        if (_compilationCache.TryGetValue(cacheKey, out cachedInfo) && File.Exists(cachedInfo.AssemblyPath))
        {
          return Result<CompiledPluginInfo>.Success(cachedInfo);
        }

        return CompilePluginInternal(plugin, cacheKey, logs, cancellationToken);
      }
    });
  }

  private Result<CompiledPluginInfo> CompilePluginInternal(
      Plugin plugin,
      string cacheKey,
      List<string> logs,
      CancellationToken cancellationToken)
  {
    try
    {
      logs.Add($"Compiling plugin: {plugin.Metadata.Name}");
      _logger.LogInformation("Compiling C# plugin: {PluginName}", plugin.Metadata.Name);

      // Resolve dependencies
      var dependencyTask = _dependencyResolver.ResolveDependenciesAsync(plugin, cancellationToken);
      var dependencyResult = dependencyTask.GetAwaiter().GetResult();

      if (dependencyResult.IsFailure)
      {
        logs.Add($"Dependency resolution failed: {dependencyResult.Error.Message}");
        return Result<CompiledPluginInfo>.Failure(dependencyResult.Error);
      }

      var dependencies = dependencyResult.Value;
      logs.Add($"Resolved {dependencies.NuGetPackages.Count} NuGet packages, " +
              $"{dependencies.FileReferences.Count} file references");

      // Create build directory
      var buildId = Guid.NewGuid().ToString();
      var buildDirectory = Path.Combine(_buildOutputPath, buildId);
      Directory.CreateDirectory(buildDirectory);

      try
      {
        // Create project structure with enhanced debugging
        var projectResult = CreateProjectStructure(plugin, dependencies, buildDirectory, logs);
        if (projectResult.IsFailure)
        {
          logs.Add($"Project structure creation failed: {projectResult.Error.Message}");
          return Result<CompiledPluginInfo>.Failure(projectResult.Error);
        }

        // Log the generated project file for debugging
        var projectPath = Path.Combine(buildDirectory, "plugin.csproj");
        if (File.Exists(projectPath))
        {
          var projectContent = File.ReadAllText(projectPath);
          _logger.LogDebug("Generated project file for {PluginName}:\n{ProjectContent}", 
              plugin.Metadata.Name, projectContent);
          logs.Add("Project file generated and logged for debugging");
        }

        // Compile using .NET CLI with enhanced error capture
        var compilationResult = CompileProject(buildDirectory, logs, cancellationToken);
        if (compilationResult.IsFailure)
        {
          logs.Add($"Compilation failed: {compilationResult.Error.Message}");
          return Result<CompiledPluginInfo>.Failure(compilationResult.Error);
        }

        // Cache the compiled assembly
        var assemblyPath = Path.Combine(buildDirectory, "bin", "Release", "net8.0", "plugin.dll");
        if (!File.Exists(assemblyPath))
        {
          var error = "Compiled assembly not found at expected location";
          logs.Add(error);
          _logger.LogError("Assembly not found at {AssemblyPath} for plugin {PluginName}", 
              assemblyPath, plugin.Metadata.Name);
          return Result<CompiledPluginInfo>.Failure(Error.Failure(
              "CSharpRuntime.AssemblyNotFound", error));
        }

        var cachedAssemblyPath = Path.Combine(_compilationCachePath, $"{cacheKey}.dll");
        File.Copy(assemblyPath, cachedAssemblyPath, true);

        var compiledInfo = new CompiledPluginInfo
        {
          PluginId = plugin.Id,
          AssemblyPath = cachedAssemblyPath,
          CacheKey = cacheKey,
          CompiledAt = DateTimeOffset.UtcNow,
          Dependencies = dependencies
        };

        _compilationCache[cacheKey] = compiledInfo;
        logs.Add($"Plugin compiled successfully: {cachedAssemblyPath}");

        return Result<CompiledPluginInfo>.Success(compiledInfo);
      }
      finally
      {
        // Cleanup build directory (but log if we're keeping it for debugging)
        try
        {
          if (Directory.Exists(buildDirectory))
          {
            // For debugging, you might want to comment this out temporarily
            Directory.Delete(buildDirectory, true);
            logs.Add("Build directory cleaned up");
          }
        }
        catch (Exception ex)
        {
          _logger.LogWarning(ex, "Failed to cleanup build directory: {BuildDirectory}", buildDirectory);
          logs.Add($"Warning: Failed to cleanup build directory: {ex.Message}");
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to compile plugin: {PluginName}", plugin.Metadata.Name);
      logs.Add($"Compilation exception: {ex.Message}");
      return Result<CompiledPluginInfo>.Failure(Error.Failure(
          "CSharpRuntime.CompilationFailed", $"Plugin compilation failed: {ex.Message}"));
    }
  }

  private Result CreateProjectStructure(
      Plugin plugin,
      PluginDependencyContext dependencies,
      string buildDirectory,
      List<string> logs)
  {
    try
    {
      // Copy source files
      var sourceDirectory = Path.Combine(buildDirectory, "src");
      Directory.CreateDirectory(sourceDirectory);
      
      // Enhanced file copying with better error handling
      CopyDirectory(plugin.PluginPath, sourceDirectory, logs);
      logs.Add($"Source files copied from {plugin.PluginPath} to {sourceDirectory}");

      // Verify entry point was copied
      var copiedEntryPoint = Path.Combine(sourceDirectory, plugin.EntryPoint);
      if (!File.Exists(copiedEntryPoint))
      {
        var error = $"Entry point file {plugin.EntryPoint} was not copied to build directory";
        logs.Add(error);
        return Result.Failure(Error.Failure("CSharpRuntime.EntryPointNotCopied", error));
      }

      // Create enhanced .csproj file
      var projectContent = GenerateProjectFile(plugin, dependencies, logs);
      var projectPath = Path.Combine(buildDirectory, "plugin.csproj");
      File.WriteAllText(projectPath, projectContent);
      logs.Add("Enhanced project file generated");

      return Result.Success();
    }
    catch (Exception ex)
    {
      var error = $"Failed to create project structure: {ex.Message}";
      logs.Add(error);
      _logger.LogError(ex, "Failed to create project structure for {PluginName}", plugin.Metadata.Name);
      return Result.Failure(Error.Failure("CSharpRuntime.ProjectStructureCreationFailed", error));
    }
  }

  private string GenerateProjectFile(Plugin plugin, PluginDependencyContext dependencies, List<string> logs)
  {
    var projectXml = new StringBuilder();
    projectXml.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
    projectXml.AppendLine();
    
    // Enhanced PropertyGroup with better configuration
    projectXml.AppendLine("  <PropertyGroup>");
    projectXml.AppendLine("    <TargetFramework>net8.0</TargetFramework>");
    projectXml.AppendLine("    <AssemblyName>plugin</AssemblyName>");
    projectXml.AppendLine("    <OutputType>Library</OutputType>");
    projectXml.AppendLine("    <Nullable>enable</Nullable>");
    projectXml.AppendLine("    <ImplicitUsings>enable</ImplicitUsings>");
    projectXml.AppendLine("    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>");
    projectXml.AppendLine("    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>");
    projectXml.AppendLine("    <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>");
    projectXml.AppendLine("    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>");
    projectXml.AppendLine("    <WarningLevel>3</WarningLevel>");
    projectXml.AppendLine("  </PropertyGroup>");
    projectXml.AppendLine();

    // Add NuGet package references
    if (dependencies.NuGetPackages.Any())
    {
      projectXml.AppendLine("  <ItemGroup>");
      foreach (var package in dependencies.NuGetPackages)
      {
        projectXml.AppendLine($"    <PackageReference Include=\"{package.PackageName}\" Version=\"{package.Version}\" />");
        logs.Add($"Added package reference: {package.PackageName} v{package.Version}");
      }
      projectXml.AppendLine("  </ItemGroup>");
      projectXml.AppendLine();
    }

    // Add file references
    if (dependencies.FileReferences.Any())
    {
      projectXml.AppendLine("  <ItemGroup>");
      foreach (var fileRef in dependencies.FileReferences.Where(f => f.Exists))
      {
        projectXml.AppendLine($"    <Reference Include=\"{Path.GetFileNameWithoutExtension(fileRef.FileName)}\">");
        projectXml.AppendLine($"      <HintPath>{fileRef.FilePath}</HintPath>");
        projectXml.AppendLine("    </Reference>");
        logs.Add($"Added file reference: {fileRef.FileName}");
      }
      projectXml.AppendLine("  </ItemGroup>");
      projectXml.AppendLine();
    }

    // Include source files with explicit patterns
    projectXml.AppendLine("  <ItemGroup>");
    projectXml.AppendLine("    <Compile Include=\"src/**/*.cs\" />");
    projectXml.AppendLine("  </ItemGroup>");
    projectXml.AppendLine();

    projectXml.AppendLine("</Project>");
    
    var result = projectXml.ToString();
    logs.Add($"Generated project file with {dependencies.NuGetPackages.Count} package references and {dependencies.FileReferences.Count} file references");
    
    return result;
  }

  private Result CompileProject(string buildDirectory, List<string> logs, CancellationToken cancellationToken)
  {
    try
    {
      var dotnetPath = FindDotNetExecutable();
      if (string.IsNullOrEmpty(dotnetPath))
      {
        var error = ".NET CLI not found in PATH. Cannot compile plugin.";
        logs.Add(error);
        return Result.Failure(Error.Failure("CSharpRuntime.DotNetNotFound", error));
      }

      logs.Add($"Using .NET CLI: {dotnetPath}");

      // Step 1: Restore packages with detailed output
      logs.Add("Starting NuGet package restore...");
      var restoreResult = RunDotNetCommandWithDetailedOutput(
          "restore", buildDirectory, logs, cancellationToken);
      
      if (restoreResult.IsFailure)
      {
        logs.Add($"Package restore failed: {restoreResult.Error.Message}");
        return restoreResult;
      }

      logs.Add("Package restore completed successfully");

      // Step 2: Build with detailed output
      logs.Add("Starting compilation...");
      var buildResult = RunDotNetCommandWithDetailedOutput(
          "build --configuration Release --no-restore --verbosity normal", 
          buildDirectory, logs, cancellationToken);

      if (buildResult.IsFailure)
      {
        logs.Add($"Build failed: {buildResult.Error.Message}");
        return buildResult;
      }

      logs.Add("Compilation completed successfully");
      return Result.Success();
    }
    catch (Exception ex)
    {
      var error = $"Compilation process failed with exception: {ex.Message}";
      logs.Add(error);
      _logger.LogError(ex, "Compilation process failed for directory: {BuildDirectory}", buildDirectory);
      return Result.Failure(Error.Failure("CSharpRuntime.CompilationException", error));
    }
  }

  private Result RunDotNetCommandWithDetailedOutput(
      string arguments, 
      string workingDirectory, 
      List<string> logs, 
      CancellationToken cancellationToken)
  {
    try
    {
      var dotnetPath = FindDotNetExecutable();
      logs.Add($"Executing: {dotnetPath} {arguments}");
      logs.Add($"Working directory: {workingDirectory}");

      var startInfo = new ProcessStartInfo
      {
        FileName = dotnetPath,
        Arguments = arguments,
        WorkingDirectory = workingDirectory,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
      };

      using var process = new Process { StartInfo = startInfo };
      var outputBuilder = new StringBuilder();
      var errorBuilder = new StringBuilder();

      process.OutputDataReceived += (_, e) => 
      { 
        if (e.Data != null) 
        {
          outputBuilder.AppendLine(e.Data);
          logs.Add($"STDOUT: {e.Data}");
        }
      };
      
      process.ErrorDataReceived += (_, e) => 
      { 
        if (e.Data != null) 
        {
          errorBuilder.AppendLine(e.Data);
          logs.Add($"STDERR: {e.Data}");
        }
      };

      process.Start();
      process.BeginOutputReadLine();
      process.BeginErrorReadLine();

      // Wait with timeout
      var timeout = TimeSpan.FromMinutes(2);
      if (!process.WaitForExit((int)timeout.TotalMilliseconds))
      {
        try { process.Kill(); } catch { }
        var error = $"Process timed out after {timeout.TotalMinutes} minutes";
        logs.Add(error);
        return Result.Failure(Error.Failure("CSharpRuntime.ProcessTimeout", error));
      }

      var standardOutput = outputBuilder.ToString();
      var errorOutput = errorBuilder.ToString();

      logs.Add($"Process completed with exit code: {process.ExitCode}");

      if (process.ExitCode != 0)
      {
        var combinedOutput = $"Exit Code: {process.ExitCode}\nSTDOUT:\n{standardOutput}\nSTDERR:\n{errorOutput}";
        logs.Add($"Command failed with detailed output:\n{combinedOutput}");
        
        _logger.LogError("dotnet command failed for {WorkingDirectory}. Command: {Arguments}\nOutput: {Output}", 
            workingDirectory, arguments, combinedOutput);

        return Result.Failure(Error.Failure(
            "CSharpRuntime.CommandFailed", 
            $"dotnet {arguments} failed: {combinedOutput}"));
      }

      return Result.Success();
    }
    catch (Exception ex)
    {
      var error = $"Failed to run dotnet command '{arguments}': {ex.Message}";
      logs.Add(error);
      _logger.LogError(ex, "Failed to run dotnet command: {Arguments}", arguments);
      return Result.Failure(Error.Failure("CSharpRuntime.CommandException", error));
    }
  }

  private Result TestDotNetCli(string dotnetPath)
  {
    try
    {
      _logger.LogDebug("Testing .NET CLI functionality at: {DotNetPath}", dotnetPath);

      var startInfo = new ProcessStartInfo
      {
        FileName = dotnetPath,
        Arguments = "--version",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
      };

      using var process = new Process { StartInfo = startInfo };
      process.Start();
      var output = process.StandardOutput.ReadToEnd();
      var error = process.StandardError.ReadToEnd();
      process.WaitForExit(10000); // 10 second timeout

      if (process.ExitCode != 0)
      {
        var errorMsg = $".NET CLI test failed. Exit code: {process.ExitCode}, Error: {error}";
        _logger.LogError(errorMsg);
        return Result.Failure(Error.Failure("CSharpRuntime.DotNetTestFailed", errorMsg));
      }

      _logger.LogInformation(".NET CLI test successful. Version: {Version}", output.Trim());
      return Result.Success();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to test .NET CLI");
      return Result.Failure(Error.Failure("CSharpRuntime.DotNetTestException", ex.Message));
    }
  }

  private void CopyDirectory(string sourceDir, string destDir, List<string> logs)
  {
    try
    {
      Directory.CreateDirectory(destDir);
      var filesCopied = 0;

      foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
      {
        var relativePath = Path.GetRelativePath(sourceDir, file);
        var destFile = Path.Combine(destDir, relativePath);

        var destFileDir = Path.GetDirectoryName(destFile);
        if (!string.IsNullOrEmpty(destFileDir))
        {
          Directory.CreateDirectory(destFileDir);
        }

        File.Copy(file, destFile, true);
        filesCopied++;
        
        // Log key files
        if (relativePath.EndsWith(".cs") || relativePath.EndsWith(".json"))
        {
          logs.Add($"Copied: {relativePath}");
        }
      }

      logs.Add($"Total files copied: {filesCopied}");
    }
    catch (Exception ex)
    {
      logs.Add($"Error copying directory: {ex.Message}");
      throw;
    }
  }

  // Helper methods
  private string GeneratePluginCacheKey(Plugin plugin)
  {
    var input = $"{plugin.Id.Value}_{plugin.Metadata.Version}_{GetPluginContentHash(plugin)}";
    using var sha256 = SHA256.Create();
    var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
    return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
  }

  private string GetPluginContentHash(Plugin plugin)
  {
    try
    {
      using var sha256 = SHA256.Create();
      var entryPointPath = Path.Combine(plugin.PluginPath, plugin.EntryPoint);

      if (File.Exists(entryPointPath))
      {
        var content = File.ReadAllBytes(entryPointPath);
        var hashBytes = sha256.ComputeHash(content);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
      }
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to compute content hash for plugin: {PluginName}", plugin.Metadata.Name);
    }

    return "unknown";
  }

  private static string? FindDotNetExecutable()
  {
    var fileName = Environment.OSVersion.Platform == PlatformID.Win32NT ? "dotnet.exe" : "dotnet";
    var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? "";

    return pathVariable.Split(Path.PathSeparator)
        .Select(path => Path.Combine(path, fileName))
        .FirstOrDefault(File.Exists);
  }

  private void CleanupOldBuildArtifacts()
  {
    try
    {
      if (Directory.Exists(_buildOutputPath))
      {
        var cutoffTime = DateTime.UtcNow.AddHours(-1);
        foreach (var dir in Directory.GetDirectories(_buildOutputPath))
        {
          if (Directory.GetCreationTimeUtc(dir) < cutoffTime)
          {
            try
            {
              Directory.Delete(dir, true);
            }
            catch (Exception ex)
            {
              _logger.LogWarning(ex, "Failed to cleanup old build directory: {Directory}", dir);
            }
          }
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to cleanup old build artifacts");
    }
  }

  public void Dispose()
  {
    if (_disposed) return;

    try
    {
      // Cleanup all load contexts
      foreach (var contextRef in _loadContexts.Values)
      {
        if (contextRef.TryGetTarget(out var loadContext))
        {
          try
          {
            loadContext.Unload();
          }
          catch (Exception ex)
          {
            _logger.LogWarning(ex, "Failed to unload assembly load context during dispose");
          }
        }
      }
      _loadContexts.Clear();
      _compilationCache.Clear();

      _logger.LogInformation("Enhanced C# runtime manager disposed successfully");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to dispose Enhanced C# runtime manager");
    }
    finally
    {
      _disposed = true;
    }
  }

  /// <summary>
  /// Isolated assembly load context for plugin execution.
  /// Provides dependency resolution and isolation from the host application.
  /// </summary>
  public sealed class PluginAssemblyLoadContext : AssemblyLoadContext
  {
    private readonly PluginDependencyContext _dependencies;
    private readonly Dictionary<string, string> _assemblyPaths;

    public PluginAssemblyLoadContext(string name, PluginDependencyContext dependencies)
        : base(name, true)
    {
      _dependencies = dependencies;
      _assemblyPaths = BuildAssemblyPathLookup();
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
      // Try to resolve from plugin dependencies first
      if (_assemblyPaths.TryGetValue(assemblyName.Name ?? "", out var assemblyPath))
      {
        if (File.Exists(assemblyPath))
        {
          return LoadFromAssemblyPath(assemblyPath);
        }
      }

      // Let the default context handle standard .NET assemblies
      return null;
    }

    private Dictionary<string, string> BuildAssemblyPathLookup()
    {
      var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

      // Add NuGet package assemblies
      foreach (var package in _dependencies.NuGetPackages)
      {
        foreach (var assemblyPath in package.AssemblyPaths)
        {
          var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
          if (!lookup.ContainsKey(assemblyName))
          {
            lookup[assemblyName] = assemblyPath;
          }
        }

        foreach (var runtimePath in package.RuntimeDependencies)
        {
          var assemblyName = Path.GetFileNameWithoutExtension(runtimePath);
          if (!lookup.ContainsKey(assemblyName))
          {
            lookup[assemblyName] = runtimePath;
          }
        }
      }

      // Add file references
      foreach (var fileRef in _dependencies.FileReferences.Where(f => f.Exists))
      {
        var assemblyName = Path.GetFileNameWithoutExtension(fileRef.FileName);
        if (!lookup.ContainsKey(assemblyName))
        {
          lookup[assemblyName] = fileRef.FilePath;
        }
      }

      return lookup;
    }
  }
}