using DevFlow.Application.Plugins.Runtime;
using DevFlow.Application.Plugins.Runtime.Models;
using DevFlow.Domain.Plugins.Entities;
using DevFlow.Domain.Plugins.Enums;
using DevFlow.SharedKernel.Results;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;

namespace DevFlow.Infrastructure.Plugins.Runtime;

/// <summary>
/// Runtime manager for executing C# plugins using in-process compilation and execution.
/// Uses Roslyn for dynamic compilation and isolated AssemblyLoadContext for plugin isolation.
/// </summary>
public sealed class CSharpRuntimeManager : IPluginRuntimeManager
{
  private readonly ILogger<CSharpRuntimeManager> _logger;
  private readonly Dictionary<string, WeakReference<AssemblyLoadContext>> _loadContexts;
  private readonly object _lockObject = new();

  public CSharpRuntimeManager(ILogger<CSharpRuntimeManager> logger)
  {
    _logger = logger;
    _loadContexts = new Dictionary<string, WeakReference<AssemblyLoadContext>>();
  }

  public IReadOnlyList<PluginLanguage> SupportedLanguages { get; } = new[] { PluginLanguage.CSharp };

  public string RuntimeId => "csharp-runtime";

  public async Task<Result<PluginExecutionResult>> ExecuteAsync(
      Plugin plugin,
      PluginExecutionContext context,
      CancellationToken cancellationToken = default)
  {
    if (plugin is null)
      return Result<PluginExecutionResult>.Failure(Error.Validation(
          "CSharpRuntime.PluginNull", "Plugin cannot be null."));

    if (context is null)
      return Result<PluginExecutionResult>.Failure(Error.Validation(
          "CSharpRuntime.ContextNull", "Execution context cannot be null."));

    if (!CanExecutePlugin(plugin))
      return Result<PluginExecutionResult>.Failure(Error.Validation(
          "CSharpRuntime.UnsupportedPlugin", $"Plugin language '{plugin.Metadata.Language}' is not supported by C# runtime."));

    var startTime = DateTimeOffset.UtcNow;
    var logs = new List<string>();
    var stopwatch = Stopwatch.StartNew();

    try
    {
      _logger.LogDebug("Starting execution of C# plugin: {PluginName}", plugin.Metadata.Name);
      logs.Add($"Starting C# plugin execution: {plugin.Metadata.Name} v{plugin.Metadata.Version}");

      // Compile the plugin
      var compilationResult = await CompilePluginAsync(plugin, logs, cancellationToken);
      if (compilationResult.IsFailure)
        return Result<PluginExecutionResult>.Failure(compilationResult.Error);

      var assembly = compilationResult.Value;
      logs.Add("Plugin compilation successful");

      // Find and instantiate the plugin class
      var pluginInstanceResult = CreatePluginInstance(assembly, plugin, logs);
      if (pluginInstanceResult.IsFailure)
        return Result<PluginExecutionResult>.Failure(pluginInstanceResult.Error);

      var pluginInstance = pluginInstanceResult.Value;
      logs.Add("Plugin instance created successfully");

      // Execute the plugin with timeout
      using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
      timeoutCts.CancelAfter(context.ExecutionTimeout);

      var executionResult = await ExecutePluginInstanceAsync(pluginInstance, context, logs, timeoutCts.Token);

      stopwatch.Stop();
      var endTime = DateTimeOffset.UtcNow;

      if (executionResult.IsFailure)
      {
        logs.Add($"Plugin execution failed: {executionResult.Error.Message}");
        return Result<PluginExecutionResult>.Success(PluginExecutionResult.Failure(
            executionResult.Error, startTime, endTime, logs, peakMemoryUsageBytes: GetCurrentMemoryUsage()));
      }

      logs.Add($"Plugin execution completed successfully in {stopwatch.ElapsedMilliseconds}ms");
      _logger.LogDebug("C# plugin execution completed: {PluginName} in {Duration}ms",
          plugin.Metadata.Name, stopwatch.ElapsedMilliseconds);

      return Result<PluginExecutionResult>.Success(PluginExecutionResult.Success(
          executionResult.Value, startTime, endTime, logs, GetCurrentMemoryUsage()));
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      stopwatch.Stop();
      logs.Add("Plugin execution was cancelled");
      _logger.LogWarning("C# plugin execution cancelled: {PluginName}", plugin.Metadata.Name);

      var error = Error.Failure("CSharpRuntime.ExecutionCancelled", "Plugin execution was cancelled.");
      return Result<PluginExecutionResult>.Success(PluginExecutionResult.Failure(
          error, startTime, DateTimeOffset.UtcNow, logs, peakMemoryUsageBytes: GetCurrentMemoryUsage()));
    }
    catch (Exception ex)
    {
      stopwatch.Stop();
      logs.Add($"Unhandled exception during plugin execution: {ex.Message}");
      _logger.LogError(ex, "Unhandled exception during C# plugin execution: {PluginName}", plugin.Metadata.Name);

      return Result<PluginExecutionResult>.Success(PluginExecutionResult.Failure(
          ex, startTime, DateTimeOffset.UtcNow, logs));
    }
  }

  public Task<Result<bool>> ValidatePluginAsync(
      Plugin plugin,
      CancellationToken cancellationToken = default)
  {
    if (plugin is null)
      return Task.FromResult(Result<bool>.Failure(Error.Validation(
          "CSharpRuntime.PluginNull", "Plugin cannot be null.")));

    if (!CanExecutePlugin(plugin))
      return Task.FromResult(Result<bool>.Failure(Error.Validation(
          "CSharpRuntime.UnsupportedPlugin", $"Plugin language '{plugin.Metadata.Language}' is not supported by C# runtime.")));

    try
    {
      var entryPointPath = Path.Combine(plugin.PluginPath, plugin.EntryPoint);
      if (!File.Exists(entryPointPath))
        return Task.FromResult(Result<bool>.Failure(Error.Validation(
            "CSharpRuntime.EntryPointNotFound", $"Entry point file '{entryPointPath}' does not exist.")));

      if (!entryPointPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        return Task.FromResult(Result<bool>.Failure(Error.Validation(
            "CSharpRuntime.InvalidEntryPoint", "C# plugin entry point must be a .cs file.")));

      // Additional validation could include syntax checking, but we'll do that during compilation
      return Task.FromResult(Result<bool>.Success(true));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to validate C# plugin: {PluginName}", plugin.Metadata.Name);
      return Task.FromResult(Result<bool>.Failure(Error.Failure(
          "CSharpRuntime.ValidationFailed", $"Plugin validation failed: {ex.Message}")));
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
      _logger.LogInformation("Initializing C# runtime manager");

      // Verify Roslyn is available
      var compilation = CSharpCompilation.Create("test");
      if (compilation is null)
        return Task.FromResult(Result.Failure(Error.Failure(
            "CSharpRuntime.RoslynUnavailable", "Roslyn compiler is not available.")));

      _logger.LogInformation("C# runtime manager initialized successfully");
      return Task.FromResult(Result.Success());
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to initialize C# runtime manager");
      return Task.FromResult(Result.Failure(Error.Failure(
          "CSharpRuntime.InitializationFailed", $"Runtime initialization failed: {ex.Message}")));
    }
  }

  public Task DisposeAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      _logger.LogInformation("Disposing C# runtime manager");

      lock (_lockObject)
      {
        foreach (var contextRef in _loadContexts.Values)
        {
          if (contextRef.TryGetTarget(out var context))
          {
            try
            {
              context.Unload();
            }
            catch (Exception ex)
            {
              _logger.LogWarning(ex, "Failed to unload assembly load context");
            }
          }
        }
        _loadContexts.Clear();
      }

      _logger.LogInformation("C# runtime manager disposed successfully");
      return Task.CompletedTask;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to dispose C# runtime manager");
      return Task.CompletedTask;
    }
  }

  private async Task<Result<Assembly>> CompilePluginAsync(
      Plugin plugin,
      List<string> logs,
      CancellationToken cancellationToken)
  {
    try
    {
      var entryPointPath = Path.Combine(plugin.PluginPath, plugin.EntryPoint);
      var sourceCode = await File.ReadAllTextAsync(entryPointPath, cancellationToken);

      logs.Add($"Read source code from: {entryPointPath}");

      // Create syntax tree
      var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, path: entryPointPath);

      // Get references to required assemblies
      var references = GetRequiredReferences();

      // Create compilation
      var compilation = CSharpCompilation.Create(
          assemblyName: $"{plugin.Metadata.Name}_{Guid.NewGuid():N}",
          syntaxTrees: new[] { syntaxTree },
          references: references,
          options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

      // Compile to memory
      using var memoryStream = new MemoryStream();
      var emitResult = compilation.Emit(memoryStream);

      if (!emitResult.Success)
      {
        var errors = emitResult.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.ToString())
            .ToList();

        logs.AddRange(errors.Select(e => $"Compilation error: {e}"));

        return Result<Assembly>.Failure(Error.Failure(
            "CSharpRuntime.CompilationFailed",
            $"Plugin compilation failed: {string.Join("; ", errors)}"));
      }

      // Load assembly into isolated context
      memoryStream.Seek(0, SeekOrigin.Begin);
      var loadContext = new PluginAssemblyLoadContext(plugin.Metadata.Name);
      var assembly = loadContext.LoadFromStream(memoryStream);

      // Store reference to load context for cleanup
      lock (_lockObject)
      {
        _loadContexts[plugin.Id.Value.ToString()] = new WeakReference<AssemblyLoadContext>(loadContext);
      }

      logs.Add("Assembly loaded successfully");
      return Result<Assembly>.Success(assembly);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to compile plugin: {PluginName}", plugin.Metadata.Name);
      return Result<Assembly>.Failure(Error.Failure(
          "CSharpRuntime.CompilationException", $"Compilation failed with exception: {ex.Message}"));
    }
  }

  private Result<object> CreatePluginInstance(Assembly assembly, Plugin plugin, List<string> logs)
  {
    try
    {
      // Look for types that implement a plugin interface, have specific attributes, or contain "Plugin" in the name
      var pluginTypes = assembly.GetTypes()
          .Where(t => t.IsClass && !t.IsAbstract)
          .Where(t => HasPluginInterface(t) || HasPluginAttribute(t) || t.Name.Contains("Plugin", StringComparison.OrdinalIgnoreCase))
          .ToList();

      if (!pluginTypes.Any())
      {
        return Result<object>.Failure(Error.Validation(
            "CSharpRuntime.NoPluginClass", "No plugin class found that implements required interfaces, attributes, or contains 'Plugin' in the name."));
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
      _logger.LogError(ex, "Failed to create plugin instance: {PluginName}", plugin.Metadata.Name);
      return Result<object>.Failure(Error.Failure(
          "CSharpRuntime.InstanceException", $"Plugin instance creation failed: {ex.Message}"));
    }
  }

  private async Task<Result<object?>> ExecutePluginInstanceAsync(
      object pluginInstance,
      PluginExecutionContext context,
      List<string> logs,
      CancellationToken cancellationToken)
  {
    try
    {
      // Look for Execute method with proper signature
      var executeMethod = pluginInstance.GetType().GetMethod("Execute") ??
                         pluginInstance.GetType().GetMethod("ExecuteAsync");

      if (executeMethod is null)
      {
        return Result<object?>.Failure(Error.Validation(
            "CSharpRuntime.NoExecuteMethod", "Plugin class must have an Execute or ExecuteAsync method."));
      }

      logs.Add($"Invoking method: {executeMethod.Name}");

      object? result;
      if (executeMethod.Name == "ExecuteAsync")
      {
        // Handle async execution - create dynamic context object
        var dynamicContext = CreateDynamicContext(context);
        var task = (Task)executeMethod.Invoke(pluginInstance, new object[] { dynamicContext, cancellationToken })!;
        await task;

        // Get result from Task<T> if it returns a value
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
        // Handle synchronous execution - create dynamic context object
        var dynamicContext = CreateDynamicContext(context);
        result = executeMethod.Invoke(pluginInstance, new object[] { dynamicContext });
      }

      logs.Add("Plugin method execution completed");
      return Result<object?>.Success(result);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to execute plugin instance");
      return Result<object?>.Failure(Error.Failure(
          "CSharpRuntime.ExecutionException", $"Plugin execution failed: {ex.Message}"));
    }
  }

  private static List<MetadataReference> GetRequiredReferences()
  {
    var references = new List<MetadataReference>();

    // Add core .NET references
    var coreAssemblyPath = typeof(object).Assembly.Location;
    var coreDirectory = Path.GetDirectoryName(coreAssemblyPath)!;

    // Core system assemblies
    references.Add(MetadataReference.CreateFromFile(coreAssemblyPath));
    references.Add(MetadataReference.CreateFromFile(Path.Combine(coreDirectory, "System.Runtime.dll")));
    references.Add(MetadataReference.CreateFromFile(Path.Combine(coreDirectory, "System.Collections.dll")));
    references.Add(MetadataReference.CreateFromFile(Path.Combine(coreDirectory, "System.Linq.dll")));
    references.Add(MetadataReference.CreateFromFile(Path.Combine(coreDirectory, "System.Threading.Tasks.dll")));
    
    // Add netstandard which contains core types
    var netstandardPath = Path.Combine(coreDirectory, "netstandard.dll");
    if (File.Exists(netstandardPath))
    {
      references.Add(MetadataReference.CreateFromFile(netstandardPath));
    }
    
    // Add System.Text.Json for JSON serialization
    var jsonAssemblyPath = Path.Combine(coreDirectory, "System.Text.Json.dll");
    if (File.Exists(jsonAssemblyPath))
    {
      references.Add(MetadataReference.CreateFromFile(jsonAssemblyPath));
    }
    
    // Add Microsoft.CSharp for dynamic support
    var csharpAssemblyPath = Path.Combine(coreDirectory, "Microsoft.CSharp.dll");
    if (File.Exists(csharpAssemblyPath))
    {
      references.Add(MetadataReference.CreateFromFile(csharpAssemblyPath));
    }
    
    // Add additional commonly needed assemblies
    var additionalAssemblies = new[]
    {
      "System.Console.dll",
      "System.IO.FileSystem.dll",
      "System.Memory.dll",
      "System.ComponentModel.Primitives.dll",
      "System.ObjectModel.dll",
      "System.Threading.dll",
      "System.Diagnostics.Process.dll",
      "System.Reflection.dll",
      "System.Runtime.CompilerServices.Unsafe.dll",
      "System.Runtime.CompilerServices.VisualC.dll",
      "System.Dynamic.Runtime.dll",
      "System.Runtime.Extensions.dll",
      "System.Core.dll"
    };
    
    foreach (var assemblyName in additionalAssemblies)
    {
      var assemblyPath = Path.Combine(coreDirectory, assemblyName);
      if (File.Exists(assemblyPath))
      {
        references.Add(MetadataReference.CreateFromFile(assemblyPath));
      }
    }

    return references;
  }

  private static bool HasPluginInterface(Type type)
  {
    // Check for common plugin interface patterns
    var interfaces = type.GetInterfaces();
    return interfaces.Any(i => i.Name.Contains("Plugin", StringComparison.OrdinalIgnoreCase));
  }

  private static bool HasPluginAttribute(Type type)
  {
    // Check for plugin attributes
    var attributes = type.GetCustomAttributes(true);
    return attributes.Any(a => a.GetType().Name.Contains("Plugin", StringComparison.OrdinalIgnoreCase));
  }

  private static long GetCurrentMemoryUsage()
  {
    return GC.GetTotalMemory(false);
  }
  
  private static object CreateDynamicContext(PluginExecutionContext context)
  {
    // Convert context to dynamic object that the plugin can use
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

  private sealed class PluginAssemblyLoadContext : AssemblyLoadContext
  {
    public PluginAssemblyLoadContext(string name) : base(name, true)
    {
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
      // Let the default context handle standard .NET assemblies
      return null;
    }
  }
}