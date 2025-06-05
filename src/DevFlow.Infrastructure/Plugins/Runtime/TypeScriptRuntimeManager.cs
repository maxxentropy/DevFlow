using DevFlow.Application.Plugins.Runtime;
using DevFlow.Application.Plugins.Runtime.Models;
using DevFlow.Domain.Plugins.Entities;
using DevFlow.Domain.Plugins.Enums;
using DevFlow.SharedKernel.Results;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace DevFlow.Infrastructure.Plugins.Runtime;

/// <summary>
/// Runtime manager for executing TypeScript plugins using Node.js runtime.
/// Handles TypeScript compilation and execution in isolated Node.js processes.
/// </summary>
public sealed class TypeScriptRuntimeManager : IPluginRuntimeManager
{
  private readonly ILogger<TypeScriptRuntimeManager> _logger;
  private bool _isNodeJsAvailable;
  private bool _isTypeScriptAvailable;
  private string? _nodeJsPath;
  private string? _npmPath;

  public TypeScriptRuntimeManager(ILogger<TypeScriptRuntimeManager> logger)
  {
    _logger = logger;
  }

  public IReadOnlyList<PluginLanguage> SupportedLanguages { get; } = new[] { PluginLanguage.TypeScript };

  public string RuntimeId => "typescript-runtime";

  public async Task<Result<PluginExecutionResult>> ExecuteAsync(
      Plugin plugin,
      PluginExecutionContext context,
      CancellationToken cancellationToken = default)
  {
    if (plugin is null)
      return Result<PluginExecutionResult>.Failure(Error.Validation(
          "TypeScriptRuntime.PluginNull", "Plugin cannot be null."));

    if (context is null)
      return Result<PluginExecutionResult>.Failure(Error.Validation(
          "TypeScriptRuntime.ContextNull", "Execution context cannot be null."));

    if (!CanExecutePlugin(plugin))
      return Result<PluginExecutionResult>.Failure(Error.Validation(
          "TypeScriptRuntime.UnsupportedPlugin", $"Plugin language '{plugin.Metadata.Language}' is not supported by TypeScript runtime."));

    if (!_isNodeJsAvailable)
      return Result<PluginExecutionResult>.Failure(Error.Failure(
          "TypeScriptRuntime.NodeJsUnavailable", "Node.js runtime is not available."));

    var startTime = DateTimeOffset.UtcNow;
    var logs = new List<string>();
    var stopwatch = Stopwatch.StartNew();

    try
    {
      _logger.LogDebug("Starting execution of TypeScript plugin: {PluginName}", plugin.Metadata.Name);
      logs.Add($"Starting TypeScript plugin execution: {plugin.Metadata.Name} v{plugin.Metadata.Version}");

      // Prepare plugin working directory
      var workingDir = await PreparePluginEnvironmentAsync(plugin, logs, cancellationToken);
      if (workingDir.IsFailure)
        return Result<PluginExecutionResult>.Failure(workingDir.Error);

      // Install dependencies if needed
      var dependencyResult = await InstallDependenciesAsync(plugin, workingDir.Value, logs, cancellationToken);
      if (dependencyResult.IsFailure)
        return Result<PluginExecutionResult>.Failure(dependencyResult.Error);

      // Compile TypeScript if needed
      var compileResult = await CompileTypeScriptAsync(plugin, workingDir.Value, logs, cancellationToken);
      if (compileResult.IsFailure)
        return Result<PluginExecutionResult>.Failure(compileResult.Error);

      // Execute the plugin
      using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
      timeoutCts.CancelAfter(context.ExecutionTimeout);

      var executionResult = await ExecuteNodeJsProcessAsync(
          plugin, workingDir.Value, context, logs, timeoutCts.Token);

      stopwatch.Stop();
      var endTime = DateTimeOffset.UtcNow;

      if (executionResult.IsFailure)
      {
        logs.Add($"Plugin execution failed: {executionResult.Error.Message}");
        return Result<PluginExecutionResult>.Success(PluginExecutionResult.Failure(
            executionResult.Error, startTime, endTime, logs, peakMemoryUsageBytes: GetEstimatedMemoryUsage()));
      }

      logs.Add($"Plugin execution completed successfully in {stopwatch.ElapsedMilliseconds}ms");
      _logger.LogDebug("TypeScript plugin execution completed: {PluginName} in {Duration}ms",
          plugin.Metadata.Name, stopwatch.ElapsedMilliseconds);

      return Result<PluginExecutionResult>.Success(PluginExecutionResult.Success(
          executionResult.Value, startTime, endTime, logs, GetEstimatedMemoryUsage()));
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      stopwatch.Stop();
      logs.Add("Plugin execution was cancelled");
      _logger.LogWarning("TypeScript plugin execution cancelled: {PluginName}", plugin.Metadata.Name);

      var error = Error.Failure("TypeScriptRuntime.ExecutionCancelled", "Plugin execution was cancelled.");
      return Result<PluginExecutionResult>.Success(PluginExecutionResult.Failure(
          error, startTime, DateTimeOffset.UtcNow, logs, peakMemoryUsageBytes: GetEstimatedMemoryUsage()));
    }
    catch (Exception ex)
    {
      stopwatch.Stop();
      logs.Add($"Unhandled exception during plugin execution: {ex.Message}");
      _logger.LogError(ex, "Unhandled exception during TypeScript plugin execution: {PluginName}", plugin.Metadata.Name);

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
          "TypeScriptRuntime.PluginNull", "Plugin cannot be null.")));

    if (!CanExecutePlugin(plugin))
      return Task.FromResult(Result<bool>.Failure(Error.Validation(
          "TypeScriptRuntime.UnsupportedPlugin", $"Plugin language '{plugin.Metadata.Language}' is not supported by TypeScript runtime.")));

    try
    {
      var entryPointPath = Path.Combine(plugin.PluginPath, plugin.EntryPoint);
      if (!File.Exists(entryPointPath))
        return Task.FromResult(Result<bool>.Failure(Error.Validation(
            "TypeScriptRuntime.EntryPointNotFound", $"Entry point file '{entryPointPath}' does not exist.")));

      if (!entryPointPath.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) &&
          !entryPointPath.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
        return Task.FromResult(Result<bool>.Failure(Error.Validation(
            "TypeScriptRuntime.InvalidEntryPoint", "TypeScript plugin entry point must be a .ts or .js file.")));

      // Check for package.json
      var packageJsonPath = Path.Combine(plugin.PluginPath, "package.json");
      if (!File.Exists(packageJsonPath))
        return Task.FromResult(Result<bool>.Failure(Error.Validation(
            "TypeScriptRuntime.PackageJsonNotFound", "TypeScript plugin must have a package.json file.")));

      return Task.FromResult(Result<bool>.Success(true));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to validate TypeScript plugin: {PluginName}", plugin.Metadata.Name);
      return Task.FromResult(Result<bool>.Failure(Error.Failure(
          "TypeScriptRuntime.ValidationFailed", $"Plugin validation failed: {ex.Message}")));
    }
  }

  public bool CanExecutePlugin(Plugin plugin)
  {
    return plugin?.Metadata?.Language == PluginLanguage.TypeScript && _isNodeJsAvailable;
  }

  public async Task<Result> InitializeAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      _logger.LogInformation("Initializing TypeScript runtime manager");

      // Check for Node.js availability
      var nodeJsResult = await CheckNodeJsAvailabilityAsync();
      if (nodeJsResult.IsFailure)
      {
        _logger.LogWarning("Node.js not available: {Error}", nodeJsResult.Error.Message);
        _isNodeJsAvailable = false;
        return Result.Success(); // Don't fail initialization, just mark as unavailable
      }

      _nodeJsPath = nodeJsResult.Value.NodePath;
      _npmPath = nodeJsResult.Value.NpmPath;
      _isNodeJsAvailable = true;

      // Check for TypeScript availability
      var typeScriptResult = await CheckTypeScriptAvailabilityAsync();
      _isTypeScriptAvailable = typeScriptResult.IsSuccess;

      _logger.LogInformation("TypeScript runtime manager initialized successfully. Node.js: {NodeAvailable}, TypeScript: {TsAvailable}",
          _isNodeJsAvailable, _isTypeScriptAvailable);

      return Result.Success();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to initialize TypeScript runtime manager");
      return Result.Failure(Error.Failure(
          "TypeScriptRuntime.InitializationFailed", $"Runtime initialization failed: {ex.Message}"));
    }
  }

  public Task DisposeAsync(CancellationToken cancellationToken = default)
  {
    _logger.LogInformation("Disposing TypeScript runtime manager");
    // No specific cleanup needed for TypeScript runtime
    return Task.CompletedTask;
  }

  private async Task<Result<(string NodePath, string NpmPath)>> CheckNodeJsAvailabilityAsync()
  {
    try
    {
      var nodeJsPath = await FindExecutableAsync("node");
      if (nodeJsPath is null)
        return Result<(string, string)>.Failure(Error.Failure(
            "TypeScriptRuntime.NodeJsNotFound", "Node.js executable not found in PATH."));

      var npmPath = await FindExecutableAsync("npm");
      if (npmPath is null)
        return Result<(string, string)>.Failure(Error.Failure(
            "TypeScriptRuntime.NpmNotFound", "npm executable not found in PATH."));

      // Verify Node.js version
      var versionResult = await RunProcessAsync(nodeJsPath, "--version", "", TimeSpan.FromSeconds(10));
      if (versionResult.IsFailure)
        return Result<(string, string)>.Failure(versionResult.Error);

      _logger.LogDebug("Node.js version: {Version}", versionResult.Value.Output.Trim());

      return Result<(string, string)>.Success((nodeJsPath, npmPath));
    }
    catch (Exception ex)
    {
      return Result<(string, string)>.Failure(Error.Failure(
          "TypeScriptRuntime.NodeJsCheckFailed", $"Failed to check Node.js availability: {ex.Message}"));
    }
  }

  private async Task<Result> CheckTypeScriptAvailabilityAsync()
  {
    try
    {
      var tscResult = await RunProcessAsync(_npmPath!, "list -g typescript", "", TimeSpan.FromSeconds(10));
      if (tscResult.IsSuccess && tscResult.Value.Output.Contains("typescript"))
      {
        _logger.LogDebug("TypeScript compiler available globally");
        return Result.Success();
      }

      _logger.LogDebug("TypeScript compiler not available globally - will install locally per plugin");
      return Result.Success();
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to check TypeScript availability");
      return Result.Failure(Error.Failure(
          "TypeScriptRuntime.TypeScriptCheckFailed", $"Failed to check TypeScript availability: {ex.Message}"));
    }
  }

  private async Task<Result<string>> PreparePluginEnvironmentAsync(
      Plugin plugin,
      List<string> logs,
      CancellationToken cancellationToken)
  {
    try
    {
      // Create temporary working directory
      var tempDir = Path.Combine(Path.GetTempPath(), $"devflow-plugin-{plugin.Metadata.Name.ToLowerInvariant()}-{Guid.NewGuid():N}");
      Directory.CreateDirectory(tempDir);

      // Copy plugin files to working directory
      await CopyDirectoryAsync(plugin.PluginPath, tempDir, cancellationToken);

      logs.Add($"Plugin environment prepared: {tempDir}");
      return Result<string>.Success(tempDir);
    }
    catch (Exception ex)
    {
      return Result<string>.Failure(Error.Failure(
          "TypeScriptRuntime.EnvironmentPreparationFailed", $"Failed to prepare plugin environment: {ex.Message}"));
    }
  }

  private async Task<Result> InstallDependenciesAsync(
      Plugin plugin,
      string workingDirectory,
      List<string> logs,
      CancellationToken cancellationToken)
  {
    try
    {
      var packageJsonPath = Path.Combine(workingDirectory, "package.json");
      if (!File.Exists(packageJsonPath))
      {
        // Create minimal package.json if it doesn't exist
        var minimalPackageJson = new
        {
          name = plugin.Metadata.Name.ToLowerInvariant(),
          version = plugin.Metadata.Version,
          main = plugin.EntryPoint,
          dependencies = new { }
        };

        await File.WriteAllTextAsync(packageJsonPath, JsonSerializer.Serialize(minimalPackageJson, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        logs.Add("Created minimal package.json");
      }

      // Install dependencies
      logs.Add("Installing npm dependencies...");
      var installResult = await RunProcessAsync(_npmPath!, "install", workingDirectory, TimeSpan.FromMinutes(5));
      if (installResult.IsFailure)
      {
        logs.Add($"npm install failed: {installResult.Error.Message}");
        return installResult;
      }

      logs.Add("Dependencies installed successfully");
      return Result.Success();
    }
    catch (Exception ex)
    {
      return Result.Failure(Error.Failure(
          "TypeScriptRuntime.DependencyInstallationFailed", $"Failed to install dependencies: {ex.Message}"));
    }
  }

  private async Task<Result> CompileTypeScriptAsync(
      Plugin plugin,
      string workingDirectory,
      List<string> logs,
      CancellationToken cancellationToken)
  {
    try
    {
      // Skip compilation if entry point is already JavaScript
      if (plugin.EntryPoint.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
      {
        logs.Add("JavaScript entry point detected - skipping TypeScript compilation");
        return Result.Success();
      }

      // Check if TypeScript compiler is available locally
      var tscPath = Path.Combine(workingDirectory, "node_modules", ".bin", "tsc.cmd");
      if (!File.Exists(tscPath))
      {
        tscPath = Path.Combine(workingDirectory, "node_modules", ".bin", "tsc");
      }

      if (!File.Exists(tscPath))
      {
        // Install TypeScript locally
        logs.Add("Installing TypeScript compiler...");
        var installTsResult = await RunProcessAsync(_npmPath!, "install typescript @types/node", workingDirectory, TimeSpan.FromMinutes(2));
        if (installTsResult.IsFailure)
        {
          logs.Add($"TypeScript installation failed: {installTsResult.Error.Message}");
          return installTsResult;
        }
      }

      // Compile TypeScript
      logs.Add("Compiling TypeScript...");
      var compileArgs = $"--target es2020 --module commonjs --outDir dist {plugin.EntryPoint}";
      var compileResult = await RunProcessAsync(tscPath, compileArgs, workingDirectory, TimeSpan.FromMinutes(1));
      if (compileResult.IsFailure)
      {
        logs.Add($"TypeScript compilation failed: {compileResult.Error.Message}");
        return compileResult;
      }

      logs.Add("TypeScript compilation completed");
      return Result.Success();
    }
    catch (Exception ex)
    {
      return Result.Failure(Error.Failure(
          "TypeScriptRuntime.CompilationFailed", $"TypeScript compilation failed: {ex.Message}"));
    }
  }

  private async Task<Result<object?>> ExecuteNodeJsProcessAsync(
      Plugin plugin,
      string workingDirectory,
      PluginExecutionContext context,
      List<string> logs,
      CancellationToken cancellationToken)
  {
    try
    {
      // Determine the actual entry point (compiled .js or original .js)
      var entryPoint = plugin.EntryPoint;
      if (plugin.EntryPoint.EndsWith(".ts", StringComparison.OrdinalIgnoreCase))
      {
        var compiledPath = Path.Combine("dist", Path.ChangeExtension(plugin.EntryPoint, ".js"));
        var fullCompiledPath = Path.Combine(workingDirectory, compiledPath);
        if (File.Exists(fullCompiledPath))
        {
          entryPoint = compiledPath;
        }
      }

      // Create execution context for the plugin
      var executionContext = new
      {
        inputData = context.InputData,
        executionParameters = context.ExecutionParameters,
        workingDirectory = context.WorkingDirectory,
        environmentVariables = context.EnvironmentVariables,
        correlationId = context.CorrelationId
      };

      var contextJson = JsonSerializer.Serialize(executionContext);

      // Execute the plugin
      logs.Add($"Executing Node.js process: {entryPoint}");
      var nodeScript = $"const plugin = require('./{entryPoint}'); const context = {contextJson}; if (plugin.default) {{ plugin.default.executeAsync ? plugin.default.executeAsync(context).then(console.log) : console.log(plugin.default.execute ? plugin.default.execute(context) : plugin.default(context)); }} else {{ plugin.executeAsync ? plugin.executeAsync(context).then(console.log) : console.log(plugin.execute ? plugin.execute(context) : plugin(context)); }}";
      var executeResult = await RunProcessAsync(_nodeJsPath!, $"-e \"{nodeScript}\"", workingDirectory, context.ExecutionTimeout);

      if (executeResult.IsFailure)
      {
        logs.Add($"Node.js execution failed: {executeResult.Error.Message}");
        return Result<object?>.Failure(executeResult.Error);
      }

      // Parse the result
      var output = executeResult.Value.Output.Trim();
      if (string.IsNullOrEmpty(output))
      {
        return Result<object?>.Success(null);
      }

      try
      {
        var result = JsonSerializer.Deserialize<object>(output);
        logs.Add("Plugin execution completed successfully");
        return Result<object?>.Success(result);
      }
      catch
      {
        // If not JSON, return as string
        return Result<object?>.Success(output);
      }
    }
    catch (Exception ex)
    {
      return Result<object?>.Failure(Error.Failure(
          "TypeScriptRuntime.ExecutionFailed", $"Plugin execution failed: {ex.Message}"));
    }
  }

  private async Task<string?> FindExecutableAsync(string fileName)
  {
    return await Task.Run(() =>
    {
      var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? "";
      var paths = pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

      var extensions = Environment.OSVersion.Platform == PlatformID.Win32NT
          ? new[] { ".exe", ".cmd", ".bat" }
          : new[] { "" };

      foreach (var path in paths)
      {
        foreach (var extension in extensions)
        {
          var fullPath = Path.Combine(path, fileName + extension);
          if (File.Exists(fullPath))
          {
            return fullPath;
          }
        }
      }

      return null;
    });
  }

  private async Task<Result<(string Output, string Error)>> RunProcessAsync(
      string fileName,
      string arguments,
      string workingDirectory,
      TimeSpan timeout)
  {
    try
    {
      using var process = new Process();
      process.StartInfo = new ProcessStartInfo
      {
        FileName = fileName,
        Arguments = arguments,
        WorkingDirectory = workingDirectory,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
      };

      var outputBuilder = new StringBuilder();
      var errorBuilder = new StringBuilder();

      process.OutputDataReceived += (_, e) => {
        if (e.Data != null) outputBuilder.AppendLine(e.Data);
      };
      process.ErrorDataReceived += (_, e) => {
        if (e.Data != null) errorBuilder.AppendLine(e.Data);
      };

      process.Start();
      process.BeginOutputReadLine();
      process.BeginErrorReadLine();

      var completedTask = await Task.WhenAny(process.WaitForExitAsync(), Task.Delay(timeout));
      if (completedTask != process.WaitForExitAsync())
      {
        process.Kill();
        return Result<(string, string)>.Failure(Error.Failure(
            "TypeScriptRuntime.ProcessTimeout", "Process execution timed out."));
      }

      var output = outputBuilder.ToString();
      var error = errorBuilder.ToString();

      if (process.ExitCode != 0)
      {
        return Result<(string, string)>.Failure(Error.Failure(
            "TypeScriptRuntime.ProcessFailed", $"Process failed with exit code {process.ExitCode}: {error}"));
      }

      return Result<(string, string)>.Success((output, error));
    }
    catch (Exception ex)
    {
      return Result<(string, string)>.Failure(Error.Failure(
          "TypeScriptRuntime.ProcessException", $"Process execution failed: {ex.Message}"));
    }
  }

  private async Task CopyDirectoryAsync(string sourceDir, string destDir, CancellationToken cancellationToken)
  {
    await Task.Run(() =>
    {
      Directory.CreateDirectory(destDir);

      foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
      {
        cancellationToken.ThrowIfCancellationRequested();

        var relativePath = Path.GetRelativePath(sourceDir, file);
        var destFile = Path.Combine(destDir, relativePath);
        var destDirectory = Path.GetDirectoryName(destFile)!;

        Directory.CreateDirectory(destDirectory);
        File.Copy(file, destFile, true);
      }
    }, cancellationToken);
  }

  private static long GetEstimatedMemoryUsage()
  {
    // Estimate memory usage - for external processes this is not directly measurable
    return Process.GetCurrentProcess().WorkingSet64;
  }
}

// Extension method to add WaitAsync with timeout for older .NET versions
public static class ProcessExtensions
{
  public static async Task<bool> WaitForExitAsync(this Process process)
  {
    return await Task.Run(() => {
      process.WaitForExit();
      return true;
    });
  }

  public static async Task<bool> WaitAsync(this Task<bool> task, TimeSpan timeout)
  {
    using var timeoutCts = new CancellationTokenSource(timeout);
    try
    {
      return await task.WaitAsync(timeoutCts.Token);
    }
    catch (OperationCanceledException)
    {
      return false;
    }
  }
}

