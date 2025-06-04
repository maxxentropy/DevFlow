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
/// Runtime manager for executing Python plugins using Python interpreter.
/// Handles Python environment setup, dependency installation, and execution in isolated processes.
/// </summary>
public sealed class PythonRuntimeManager : IPluginRuntimeManager
{
  private readonly ILogger<PythonRuntimeManager> _logger;
  private bool _isPythonAvailable;
  private bool _isPipAvailable;
  private string? _pythonPath;
  private string? _pipPath;
  private string? _pythonVersion;

  public PythonRuntimeManager(ILogger<PythonRuntimeManager> logger)
  {
    _logger = logger;
  }

  public IReadOnlyList<PluginLanguage> SupportedLanguages { get; } = new[] { PluginLanguage.Python };

  public string RuntimeId => "python-runtime";

  public async Task<Result<PluginExecutionResult>> ExecuteAsync(
      Plugin plugin,
      PluginExecutionContext context,
      CancellationToken cancellationToken = default)
  {
    if (plugin is null)
      return Result<PluginExecutionResult>.Failure(Error.Validation(
          "PythonRuntime.PluginNull", "Plugin cannot be null."));

    if (context is null)
      return Result<PluginExecutionResult>.Failure(Error.Validation(
          "PythonRuntime.ContextNull", "Execution context cannot be null."));

    if (!CanExecutePlugin(plugin))
      return Result<PluginExecutionResult>.Failure(Error.Validation(
          "PythonRuntime.UnsupportedPlugin", $"Plugin language '{plugin.Metadata.Language}' is not supported by Python runtime."));

    if (!_isPythonAvailable)
      return Result<PluginExecutionResult>.Failure(Error.Failure(
          "PythonRuntime.PythonUnavailable", "Python interpreter is not available."));

    var startTime = DateTimeOffset.UtcNow;
    var logs = new List<string>();
    var stopwatch = Stopwatch.StartNew();

    try
    {
      _logger.LogDebug("Starting execution of Python plugin: {PluginName}", plugin.Metadata.Name);
      logs.Add($"Starting Python plugin execution: {plugin.Metadata.Name} v{plugin.Metadata.Version}");

      // Prepare plugin working directory
      var workingDir = await PreparePluginEnvironmentAsync(plugin, logs, cancellationToken);
      if (workingDir.IsFailure)
        return Result<PluginExecutionResult>.Failure(workingDir.Error);

      // Create virtual environment and install dependencies
      var envResult = await SetupPythonEnvironmentAsync(plugin, workingDir.Value, logs, cancellationToken);
      if (envResult.IsFailure)
        return Result<PluginExecutionResult>.Failure(envResult.Error);

      // Execute the plugin
      using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
      timeoutCts.CancelAfter(context.ExecutionTimeout);

      var executionResult = await ExecutePythonProcessAsync(
          plugin, workingDir.Value, envResult.Value, context, logs, timeoutCts.Token);

      stopwatch.Stop();
      var endTime = DateTimeOffset.UtcNow;

      if (executionResult.IsFailure)
      {
        logs.Add($"Plugin execution failed: {executionResult.Error.Message}");
        return Result<PluginExecutionResult>.Success(PluginExecutionResult.Failure(
            executionResult.Error, startTime, endTime, logs, peakMemoryUsageBytes: GetEstimatedMemoryUsage()));
      }

      logs.Add($"Plugin execution completed successfully in {stopwatch.ElapsedMilliseconds}ms");
      _logger.LogDebug("Python plugin execution completed: {PluginName} in {Duration}ms",
          plugin.Metadata.Name, stopwatch.ElapsedMilliseconds);

      return Result<PluginExecutionResult>.Success(PluginExecutionResult.Success(
          executionResult.Value, startTime, endTime, logs, GetEstimatedMemoryUsage()));
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      stopwatch.Stop();
      logs.Add("Plugin execution was cancelled");
      _logger.LogWarning("Python plugin execution cancelled: {PluginName}", plugin.Metadata.Name);

      var error = Error.Failure("PythonRuntime.ExecutionCancelled", "Plugin execution was cancelled.");
      return Result<PluginExecutionResult>.Success(PluginExecutionResult.Failure(
          error, startTime, DateTimeOffset.UtcNow, logs, peakMemoryUsageBytes: GetEstimatedMemoryUsage()));
    }
    catch (Exception ex)
    {
      stopwatch.Stop();
      logs.Add($"Unhandled exception during plugin execution: {ex.Message}");
      _logger.LogError(ex, "Unhandled exception during Python plugin execution: {PluginName}", plugin.Metadata.Name);

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
          "PythonRuntime.PluginNull", "Plugin cannot be null.")));

    if (!CanExecutePlugin(plugin))
      return Task.FromResult(Result<bool>.Failure(Error.Validation(
          "PythonRuntime.UnsupportedPlugin", $"Plugin language '{plugin.Metadata.Language}' is not supported by Python runtime.")));

    try
    {
      var entryPointPath = Path.Combine(plugin.PluginPath, plugin.EntryPoint);
      if (!File.Exists(entryPointPath))
        return Task.FromResult(Result<bool>.Failure(Error.Validation(
            "PythonRuntime.EntryPointNotFound", $"Entry point file '{entryPointPath}' does not exist.")));

      if (!entryPointPath.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
        return Task.FromResult(Result<bool>.Failure(Error.Validation(
            "PythonRuntime.InvalidEntryPoint", "Python plugin entry point must be a .py file.")));

      // Check for requirements.txt (optional but recommended)
      var requirementsPath = Path.Combine(plugin.PluginPath, "requirements.txt");
      if (!File.Exists(requirementsPath))
      {
        _logger.LogDebug("No requirements.txt found for plugin {PluginName}, will attempt to parse dependencies from plugin.json", plugin.Metadata.Name);
      }

      return Task.FromResult(Result<bool>.Success(true));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to validate Python plugin: {PluginName}", plugin.Metadata.Name);
      return Task.FromResult(Result<bool>.Failure(Error.Failure(
          "PythonRuntime.ValidationFailed", $"Plugin validation failed: {ex.Message}")));
    }
  }

  public bool CanExecutePlugin(Plugin plugin)
  {
    return plugin?.Metadata?.Language == PluginLanguage.Python && _isPythonAvailable;
  }

  public async Task<Result> InitializeAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      _logger.LogInformation("Initializing Python runtime manager");

      // Check for Python availability
      var pythonResult = await CheckPythonAvailabilityAsync();
      if (pythonResult.IsFailure)
      {
        _logger.LogWarning("Python not available: {Error}", pythonResult.Error.Message);
        _isPythonAvailable = false;
        return Result.Success(); // Don't fail initialization, just mark as unavailable
      }

      _pythonPath = pythonResult.Value.PythonPath;
      _pipPath = pythonResult.Value.PipPath;
      _pythonVersion = pythonResult.Value.Version;
      _isPythonAvailable = true;
      _isPipAvailable = _pipPath != null;

      _logger.LogInformation("Python runtime manager initialized successfully. Python: {PythonVersion}, Pip: {PipAvailable}",
          _pythonVersion, _isPipAvailable);

      return Result.Success();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to initialize Python runtime manager");
      return Result.Failure(Error.Failure(
          "PythonRuntime.InitializationFailed", $"Runtime initialization failed: {ex.Message}"));
    }
  }

  public Task DisposeAsync(CancellationToken cancellationToken = default)
  {
    _logger.LogInformation("Disposing Python runtime manager");
    // No specific cleanup needed for Python runtime
    return Task.CompletedTask;
  }

  private async Task<Result<(string PythonPath, string? PipPath, string Version)>> CheckPythonAvailabilityAsync()
  {
    try
    {
      // Try different Python executable names
      var pythonNames = new[] { "python", "python3", "py" };
      string? pythonPath = null;
      string? version = null;

      foreach (var pythonName in pythonNames)
      {
        var path = await FindExecutableAsync(pythonName);
        if (path != null)
        {
          // Verify it's a valid Python installation
          var versionResult = await RunProcessAsync(path, "--version", "", TimeSpan.FromSeconds(10));
          if (versionResult.IsSuccess)
          {
            pythonPath = path;
            version = versionResult.Value.Output.Trim();
            break;
          }
        }
      }

      if (pythonPath is null)
        return Result<(string, string?, string)>.Failure(Error.Failure(
            "PythonRuntime.PythonNotFound", "Python executable not found in PATH."));

      // Check for pip
      var pipPath = await FindExecutableAsync("pip") ?? await FindExecutableAsync("pip3");
      if (pipPath == null)
      {
        // Try using python -m pip
        var pipTestResult = await RunProcessAsync(pythonPath, "-m pip --version", "", TimeSpan.FromSeconds(10));
        if (pipTestResult.IsSuccess)
        {
          pipPath = $"{pythonPath} -m pip";
        }
      }

      _logger.LogDebug("Python version: {Version}", version);
      _logger.LogDebug("Pip available: {PipAvailable}", pipPath != null);

      return Result<(string, string?, string)>.Success((pythonPath, pipPath, version ?? "Unknown"));
    }
    catch (Exception ex)
    {
      return Result<(string, string?, string)>.Failure(Error.Failure(
          "PythonRuntime.PythonCheckFailed", $"Failed to check Python availability: {ex.Message}"));
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
          "PythonRuntime.EnvironmentPreparationFailed", $"Failed to prepare plugin environment: {ex.Message}"));
    }
  }

  private async Task<Result<string>> SetupPythonEnvironmentAsync(
      Plugin plugin,
      string workingDirectory,
      List<string> logs,
      CancellationToken cancellationToken)
  {
    try
    {
      // Create virtual environment
      var venvPath = Path.Combine(workingDirectory, "venv");
      logs.Add("Creating Python virtual environment...");
      
      var venvResult = await RunProcessAsync(_pythonPath!, $"-m venv {venvPath}", workingDirectory, TimeSpan.FromMinutes(2));
      if (venvResult.IsFailure)
      {
        logs.Add($"Virtual environment creation failed: {venvResult.Error.Message}");
        // Continue without virtual environment
        logs.Add("Proceeding without virtual environment");
        venvPath = workingDirectory;
      }
      else
      {
        logs.Add("Virtual environment created successfully");
      }

      // Determine the Python executable in the virtual environment
      var venvPython = GetVirtualEnvironmentPython(venvPath);
      if (!File.Exists(venvPython))
      {
        venvPython = _pythonPath!; // Fallback to system Python
        logs.Add("Using system Python instead of virtual environment");
      }

      // Install dependencies
      await InstallPythonDependenciesAsync(plugin, workingDirectory, venvPython, logs, cancellationToken);

      return Result<string>.Success(venvPython);
    }
    catch (Exception ex)
    {
      return Result<string>.Failure(Error.Failure(
          "PythonRuntime.EnvironmentSetupFailed", $"Failed to setup Python environment: {ex.Message}"));
    }
  }

  private async Task InstallPythonDependenciesAsync(
      Plugin plugin,
      string workingDirectory,
      string pythonExecutable,
      List<string> logs,
      CancellationToken cancellationToken)
  {
    try
    {
      var requirementsPath = Path.Combine(workingDirectory, "requirements.txt");
      var hasRequirements = File.Exists(requirementsPath);

      if (!hasRequirements)
      {
        // Create requirements.txt from plugin dependencies
        var dependencies = ExtractPythonDependencies(plugin);
        if (dependencies.Any())
        {
          await File.WriteAllLinesAsync(requirementsPath, dependencies, cancellationToken);
          logs.Add($"Created requirements.txt with {dependencies.Count} dependencies");
          hasRequirements = true;
        }
      }

      if (hasRequirements && _isPipAvailable)
      {
        logs.Add("Installing Python dependencies...");
        var installResult = await RunProcessAsync(pythonExecutable, $"-m pip install -r requirements.txt", workingDirectory, TimeSpan.FromMinutes(10));
        if (installResult.IsFailure)
        {
          logs.Add($"Dependency installation failed: {installResult.Error.Message}");
          logs.Add("Proceeding without dependencies - plugin may fail if dependencies are required");
        }
        else
        {
          logs.Add("Dependencies installed successfully");
        }
      }
      else if (!_isPipAvailable)
      {
        logs.Add("Pip not available - skipping dependency installation");
      }
      else
      {
        logs.Add("No dependencies to install");
      }
    }
    catch (Exception ex)
    {
      logs.Add($"Dependency installation error: {ex.Message}");
      // Don't fail - continue execution
    }
  }

  private async Task<Result<object?>> ExecutePythonProcessAsync(
      Plugin plugin,
      string workingDirectory,
      string pythonExecutable,
      PluginExecutionContext context,
      List<string> logs,
      CancellationToken cancellationToken)
  {
    try
    {
      // Create a wrapper script that handles the plugin execution
      var wrapperScript = CreatePythonWrapperScript(plugin, context);
      var wrapperPath = Path.Combine(workingDirectory, "_devflow_wrapper.py");
      await File.WriteAllTextAsync(wrapperPath, wrapperScript, cancellationToken);

      // Execute the wrapper script
      logs.Add($"Executing Python script: {plugin.EntryPoint}");
      var executeResult = await RunProcessAsync(pythonExecutable, wrapperPath, workingDirectory, context.ExecutionTimeout);

      if (executeResult.IsFailure)
      {
        logs.Add($"Python execution failed: {executeResult.Error.Message}");
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
          "PythonRuntime.ExecutionFailed", $"Plugin execution failed: {ex.Message}"));
    }
  }

  private string CreatePythonWrapperScript(Plugin plugin, PluginExecutionContext context)
  {
    var contextJson = JsonSerializer.Serialize(new
    {
      inputData = context.InputData,
      executionParameters = context.ExecutionParameters,
      workingDirectory = context.WorkingDirectory,
      environmentVariables = context.EnvironmentVariables,
      correlationId = context.CorrelationId
    });

    var script = "import sys\n" +
                 "import os\n" +
                 "import json\n" +
                 "import importlib.util\n" +
                 "import asyncio\n\n" +
                 "# Add current directory to Python path\n" +
                 "sys.path.insert(0, os.getcwd())\n\n" +
                 "def load_plugin_module():\n" +
                 "    \"\"\"Load the plugin module dynamically\"\"\"\n" +
                 "    entry_point = 'ENTRY_POINT_PLACEHOLDER'\n" +
                 "    module_name = os.path.splitext(entry_point)[0]\n" +
                 "    try:\n" +
                 "        spec = importlib.util.spec_from_file_location(module_name, entry_point)\n" +
                 "        module = importlib.util.module_from_spec(spec)\n" +
                 "        spec.loader.exec_module(module)\n" +
                 "        return module\n" +
                 "    except Exception as e:\n" +
                 "        print(f'Failed to load plugin module: {e}', file=sys.stderr)\n" +
                 "        sys.exit(1)\n\n" +
                 "def main():\n" +
                 "    plugin_module = load_plugin_module()\n" +
                 "    context = CONTEXT_JSON_PLACEHOLDER\n" +
                 "    plugin_instance = None\n" +
                 "    execute_method = None\n" +
                 "    for attr_name in dir(plugin_module):\n" +
                 "        attr = getattr(plugin_module, attr_name)\n" +
                 "        if (isinstance(attr, type) and 'Plugin' in attr_name and hasattr(attr, 'execute_async')):\n" +
                 "            plugin_instance = attr()\n" +
                 "            execute_method = plugin_instance.execute_async\n" +
                 "            break\n" +
                 "        elif (isinstance(attr, type) and 'Plugin' in attr_name and hasattr(attr, 'execute')):\n" +
                 "            plugin_instance = attr()\n" +
                 "            execute_method = plugin_instance.execute\n" +
                 "            break\n" +
                 "    if execute_method is None:\n" +
                 "        if hasattr(plugin_module, 'execute_async'):\n" +
                 "            execute_method = plugin_module.execute_async\n" +
                 "        elif hasattr(plugin_module, 'execute'):\n" +
                 "            execute_method = plugin_module.execute\n" +
                 "    if execute_method is None:\n" +
                 "        print('No execute method found in plugin', file=sys.stderr)\n" +
                 "        sys.exit(1)\n" +
                 "    try:\n" +
                 "        if asyncio.iscoroutinefunction(execute_method):\n" +
                 "            result = asyncio.run(execute_method(context))\n" +
                 "        else:\n" +
                 "            result = execute_method(context)\n" +
                 "        if result is not None:\n" +
                 "            print(json.dumps(result, default=str))\n" +
                 "    except Exception as e:\n" +
                 "        error_result = {'success': False, 'error': str(e), 'type': type(e).__name__}\n" +
                 "        print(json.dumps(error_result), file=sys.stderr)\n" +
                 "        sys.exit(1)\n\n" +
                 "if __name__ == '__main__':\n" +
                 "    main()\n";

    return script
        .Replace("ENTRY_POINT_PLACEHOLDER", plugin.EntryPoint)
        .Replace("CONTEXT_JSON_PLACEHOLDER", contextJson);
  }

  private List<string> ExtractPythonDependencies(Plugin plugin)
  {
    var dependencies = new List<string>();
    
    // Extract dependencies from plugin metadata
    //if (plugin.Dependencies?.Any() == true)
    //{
    //  foreach (var dep in plugin.Dependencies)
    //  {
    //    if (dep.StartsWith("pip:", StringComparison.OrdinalIgnoreCase))
    //    {
    //      var packageSpec = dep.Substring(4); // Remove "pip:" prefix
    //      dependencies.Add(packageSpec);
    //    }
    //  }
    //}
    
    return dependencies;
  }

  private string GetVirtualEnvironmentPython(string venvPath)
  {
    if (Environment.OSVersion.Platform == PlatformID.Win32NT)
    {
      return Path.Combine(venvPath, "Scripts", "python.exe");
    }
    else
    {
      return Path.Combine(venvPath, "bin", "python");
    }
  }

  private async Task<string?> FindExecutableAsync(string fileName)
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
            "PythonRuntime.ProcessTimeout", "Process execution timed out."));
      }

      var output = outputBuilder.ToString();
      var error = errorBuilder.ToString();

      if (process.ExitCode != 0)
      {
        return Result<(string, string)>.Failure(Error.Failure(
            "PythonRuntime.ProcessFailed", $"Process failed with exit code {process.ExitCode}: {error}"));
      }

      return Result<(string, string)>.Success((output, error));
    }
    catch (Exception ex)
    {
      return Result<(string, string)>.Failure(Error.Failure(
          "PythonRuntime.ProcessException", $"Process execution failed: {ex.Message}"));
    }
  }

  private async Task CopyDirectoryAsync(string sourceDir, string destDir, CancellationToken cancellationToken)
  {
    Directory.CreateDirectory(destDir);

    foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
    {
      var relativePath = Path.GetRelativePath(sourceDir, file);
      var destFile = Path.Combine(destDir, relativePath);
      var destDirectory = Path.GetDirectoryName(destFile)!;

      Directory.CreateDirectory(destDirectory);
      File.Copy(file, destFile, true);
    }
  }

  private static long GetEstimatedMemoryUsage()
  {
    // Estimate memory usage - for external processes this is not directly measurable
    return Process.GetCurrentProcess().WorkingSet64;
  }
}

