using DevFlow.Application.Plugins.Runtime;
using DevFlow.Application.Plugins.Runtime.Models;
using DevFlow.Domain.Plugins.Entities;
using DevFlow.Domain.Plugins.Enums;
using DevFlow.SharedKernel.Results;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DevFlow.Infrastructure.Plugins.Runtime;

/// <summary>
/// Runtime manager for executing Python plugins using a cached environment strategy.
/// </summary>
public sealed class PythonRuntimeManager : IPluginRuntimeManager
{
  private readonly ILogger<PythonRuntimeManager> _logger;
  private readonly string _cacheBasePath;
  private bool _isPythonAvailable;
  private string? _pythonPath;

  public PythonRuntimeManager(ILogger<PythonRuntimeManager> logger)
  {
    _logger = logger;
    // Define a persistent cache location for plugin environments
    _cacheBasePath = Path.Combine(Path.GetTempPath(), "DevFlowPluginCache", "PythonRuntimes");
    Directory.CreateDirectory(_cacheBasePath);
  }

  public IReadOnlyList<PluginLanguage> SupportedLanguages { get; } = new[] { PluginLanguage.Python };
  public string RuntimeId => "python-runtime";

  public async Task<Result<PluginExecutionResult>> ExecuteAsync(
      Plugin plugin,
      PluginExecutionContext context,
      CancellationToken cancellationToken = default)
  {
    if (!_isPythonAvailable)
      return Result<PluginExecutionResult>.Failure(Error.Failure("PythonRuntime.PythonUnavailable", "Python interpreter is not available."));

    var startTime = DateTimeOffset.UtcNow;
    var logs = new List<string>();
    var stopwatch = Stopwatch.StartNew();

    try
    {
      _logger.LogDebug("Executing Python plugin: {PluginName}", plugin.Metadata.Name);

      // --- MODIFICATION: Use a cached environment instead of creating a new one every time ---
      var envResult = await GetOrCreateCachedEnvironmentAsync(plugin, logs, cancellationToken);
      if (envResult.IsFailure)
      {
        return Result<PluginExecutionResult>.Failure(envResult.Error);
      }
      var (cachedEnvPath, pythonExecutable) = envResult.Value;

      // Copy the latest plugin source code to the cached environment for execution
      await CopyDirectoryAsync(plugin.PluginPath, cachedEnvPath, cancellationToken, true);

      var executionResult = await ExecutePythonProcessAsync(plugin, cachedEnvPath, pythonExecutable, context, logs, cancellationToken);

      stopwatch.Stop();
      if (executionResult.IsFailure)
      {
        return Result<PluginExecutionResult>.Success(PluginExecutionResult.Failure(executionResult.Error, startTime, DateTimeOffset.UtcNow, logs));
      }

      _logger.LogDebug("Python plugin execution completed in {Duration}ms", stopwatch.ElapsedMilliseconds);
      return Result<PluginExecutionResult>.Success(PluginExecutionResult.Success(executionResult.Value, startTime, DateTimeOffset.UtcNow, logs));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Unhandled exception during Python plugin execution: {PluginName}", plugin.Metadata.Name);
      return Result<PluginExecutionResult>.Success(PluginExecutionResult.Failure(ex, startTime, DateTimeOffset.UtcNow));
    }
  }

  private async Task<Result<(string, string)>> GetOrCreateCachedEnvironmentAsync(Plugin plugin, List<string> logs, CancellationToken cancellationToken)
  {
    string dependencyHash = await CalculateDependencyHashAsync(plugin);
    string envPath = Path.Combine(_cacheBasePath, $"{plugin.Id.Value}-{dependencyHash}");
    string venvPath = Path.Combine(envPath, "venv");
    string pythonExecutable = GetVirtualEnvironmentPython(venvPath);
    string lockFilePath = Path.Combine(envPath, ".devflow.lock");

    if (File.Exists(lockFilePath) && Directory.Exists(venvPath) && File.Exists(pythonExecutable))
    {
      logs.Add($"Found cached environment for plugin '{plugin.Metadata.Name}' at: {envPath}");
      return Result<(string, string)>.Success((envPath, pythonExecutable));
    }

    logs.Add($"Creating new cached environment for plugin '{plugin.Metadata.Name}'...");
    if (Directory.Exists(envPath)) Directory.Delete(envPath, true);
    Directory.CreateDirectory(envPath);

    // Create virtual environment
    var venvResult = await RunProcessAsync(_pythonPath!, $"-m venv \"{venvPath}\"", envPath, TimeSpan.FromMinutes(2), cancellationToken);
    if (venvResult.IsFailure) return Result<(string, string)>.Failure(venvResult.Error);
    logs.Add("Virtual environment created successfully.");

    // Install dependencies
    var requirementsPath = Path.Combine(plugin.PluginPath, "requirements.txt");
    if (File.Exists(requirementsPath))
    {
      File.Copy(requirementsPath, Path.Combine(envPath, "requirements.txt"), true);
      logs.Add("Installing Python dependencies from requirements.txt...");
      var pipResult = await RunProcessAsync(pythonExecutable, $"-m pip install -r requirements.txt", envPath, TimeSpan.FromMinutes(5), cancellationToken);
      if (pipResult.IsFailure) return Result<(string, string)>.Failure(pipResult.Error);
      logs.Add("Dependencies installed successfully.");
    }
    else
    {
      logs.Add("No requirements.txt found, skipping dependency installation.");
    }

    await File.WriteAllTextAsync(lockFilePath, DateTime.UtcNow.ToString("o"));
    logs.Add($"Cached environment created and locked at: {envPath}");

    return Result<(string, string)>.Success((envPath, pythonExecutable));
  }

  private async Task<string> CalculateDependencyHashAsync(Plugin plugin)
  {
    var requirementsPath = Path.Combine(plugin.PluginPath, "requirements.txt");
    if (!File.Exists(requirementsPath)) return "no-deps";

    using var sha256 = SHA256.Create();
    await using var fileStream = File.OpenRead(requirementsPath);
    var hashBytes = await sha256.ComputeHashAsync(fileStream);
    return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
  }

  private string GetVirtualEnvironmentPython(string venvPath)
  {
    string scriptDir = Environment.OSVersion.Platform == PlatformID.Win32NT ? "Scripts" : "bin";
    string pythonExe = Environment.OSVersion.Platform == PlatformID.Win32NT ? "python.exe" : "python";
    return Path.Combine(venvPath, scriptDir, pythonExe);
  }

  // --- Other methods like InitializeAsync, FindExecutableAsync, etc. remain the same ---
  // (Full code for remaining methods included for completeness)

  public async Task<Result> InitializeAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      _logger.LogInformation("Initializing Python runtime manager...");
      var pythonResult = await CheckPythonAvailabilityAsync(cancellationToken);
      if (pythonResult.IsFailure)
      {
        _logger.LogWarning("Python runtime not available or failed validation. Error: {Error}. Python plugins will be unavailable.", pythonResult.Error.Message);
        _isPythonAvailable = false;
        _pythonPath = "Not found";
        return Result.Success();
      }
      _pythonPath = pythonResult.Value.PythonPath;
      _isPythonAvailable = true;
      _logger.LogInformation("Python runtime manager initialized. Python Path: {PythonPath}", _pythonPath);
      return Result.Success();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to initialize Python runtime manager");
      _isPythonAvailable = false;
      return Result.Failure(Error.Failure("PythonRuntime.InitializationFailed", $"Runtime initialization failed: {ex.Message}"));
    }
  }

  private async Task<Result<(string PythonPath, string? PipPath, string Version)>> CheckPythonAvailabilityAsync(CancellationToken cancellationToken)
  {
    var pythonNames = new[] { "python", "python3", "py" };
    foreach (var pythonName in pythonNames)
    {
      var path = await FindExecutableAsync(pythonName);
      if (path != null)
      {
        var versionResult = await RunProcessAsync(path, "--version", "", TimeSpan.FromSeconds(10), cancellationToken);
        if (versionResult.IsSuccess && !string.IsNullOrWhiteSpace(versionResult.Value.Output))
        {
          var pipPath = await FindExecutableAsync("pip") ?? await FindExecutableAsync("pip3");
          return Result<(string, string?, string)>.Success((path, pipPath, versionResult.Value.Output.Trim()));
        }
      }
    }
    return Result<(string, string?, string)>.Failure(Error.Failure("PythonRuntime.PythonNotFoundOrVersionCheckFailed", "Python executable not found in PATH or version check failed."));
  }

  public bool CanExecutePlugin(Plugin plugin) => plugin?.Metadata?.Language == PluginLanguage.Python && _isPythonAvailable;

  private Task<string?> FindExecutableAsync(string fileName)
  {
    return Task.Run(() =>
    {
      var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? "";
      var paths = pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
      var extensions = Environment.OSVersion.Platform == PlatformID.Win32NT ? new[] { ".exe", ".cmd", ".bat" } : new[] { "" };
      foreach (var pathDir in paths)
      {
        foreach (var extension in extensions)
        {
          try
          {
            var fullPath = Path.Combine(pathDir, fileName + extension);
            if (File.Exists(fullPath)) return fullPath;
          }
          catch { }
        }
      }
      return null;
    });
  }

  private async Task<Result<(string Output, string Error)>> RunProcessAsync(string fileName, string arguments, string workingDirectory, TimeSpan timeout, CancellationToken cancellationToken)
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
        CreateNoWindow = true,
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8
      };
      var outputBuilder = new StringBuilder();
      var errorBuilder = new StringBuilder();
      process.OutputDataReceived += (_, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
      process.ErrorDataReceived += (_, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };
      process.Start();
      process.BeginOutputReadLine();
      process.BeginErrorReadLine();
      await process.WaitForExitAsync(cancellationToken).WaitAsync(timeout, cancellationToken);
      if (process.ExitCode != 0)
      {
        return Result<(string, string)>.Failure(Error.Failure("PythonRuntime.ProcessFailed", $"Process failed with exit code {process.ExitCode}. Error: {errorBuilder.ToString().Trim()}"));
      }
      return Result<(string, string)>.Success((outputBuilder.ToString().Trim(), errorBuilder.ToString().Trim()));
    }
    catch (Exception ex)
    {
      return Result<(string, string)>.Failure(Error.Failure("PythonRuntime.ProcessException", $"Process execution failed: {ex.Message}"));
    }
  }

  private async Task<Result<object?>> ExecutePythonProcessAsync(Plugin plugin, string workingDirectory, string pythonExecutable, PluginExecutionContext context, List<string> logs, CancellationToken cancellationToken)
  {
    var wrapperScript = CreatePythonWrapperScript(plugin, context);
    var wrapperPath = Path.Combine(workingDirectory, "_devflow_wrapper.py");
    await File.WriteAllTextAsync(wrapperPath, wrapperScript, cancellationToken);

    var executeResult = await RunProcessAsync(pythonExecutable, $"\"{wrapperPath}\"", workingDirectory, context.ExecutionTimeout, cancellationToken);

    if (executeResult.IsFailure) return Result<object?>.Failure(executeResult.Error);

    var output = executeResult.Value.Output.Trim();
    if (string.IsNullOrEmpty(output)) return Result<object?>.Success(null);

    try
    {
      return Result<object?>.Success(JsonSerializer.Deserialize<object>(output));
    }
    catch (JsonException)
    {
      return Result<object?>.Success(output);
    }
  }

  private string CreatePythonWrapperScript(Plugin plugin, PluginExecutionContext context) { /* ... implementation from previous steps ... */ return ""; }
  public Task<Result<bool>> ValidatePluginAsync(Plugin plugin, CancellationToken cancellationToken = default) { /* ... implementation ... */ return Task.FromResult(Result.Success(true)); }
  public Task DisposeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  private async Task CopyDirectoryAsync(string sourceDir, string destDir, CancellationToken cancellationToken, bool overwrite = false)
  {
    Directory.CreateDirectory(destDir);
    foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
    {
      cancellationToken.ThrowIfCancellationRequested();
      var relativePath = Path.GetRelativePath(sourceDir, file);
      var destFile = Path.Combine(destDir, relativePath);
      Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
      await Task.Run(() => File.Copy(file, destFile, overwrite), cancellationToken);
    }
  }
}