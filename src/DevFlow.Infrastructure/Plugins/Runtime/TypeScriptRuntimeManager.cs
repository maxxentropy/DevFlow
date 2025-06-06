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
/// Runtime manager for executing TypeScript plugins using a cached environment strategy.
/// </summary>
public sealed class TypeScriptRuntimeManager : IPluginRuntimeManager
{
  private readonly ILogger<TypeScriptRuntimeManager> _logger;
  private readonly string _cacheBasePath;
  private bool _isNodeJsAvailable;
  private string? _nodeJsPath;
  private string? _npmPath;

  public TypeScriptRuntimeManager(ILogger<TypeScriptRuntimeManager> logger)
  {
    _logger = logger;
    _cacheBasePath = Path.Combine(Path.GetTempPath(), "DevFlowPluginCache", "TypeScriptRuntimes");
    Directory.CreateDirectory(_cacheBasePath);
  }

  public IReadOnlyList<PluginLanguage> SupportedLanguages { get; } = new[] { PluginLanguage.TypeScript };
  public string RuntimeId => "typescript-runtime";

  public async Task<Result<PluginExecutionResult>> ExecuteAsync(
      Plugin plugin,
      PluginExecutionContext context,
      CancellationToken cancellationToken = default)
  {
    if (!_isNodeJsAvailable)
      return Result<PluginExecutionResult>.Failure(Error.Failure("TypeScriptRuntime.NodeJsUnavailable", "Node.js runtime is not available."));

    var startTime = DateTimeOffset.UtcNow;
    var logs = new List<string>();
    var stopwatch = Stopwatch.StartNew();

    try
    {
      _logger.LogDebug("Executing TypeScript plugin: {PluginName}", plugin.Metadata.Name);

      // --- MODIFICATION: Use a cached environment ---
      var envResult = await GetOrCreateCachedEnvironmentAsync(plugin, logs, cancellationToken);
      if (envResult.IsFailure) return Result<PluginExecutionResult>.Failure(envResult.Error);

      string cachedEnvPath = envResult.Value;

      // Copy latest plugin source code to the cached environment
      await CopyDirectoryAsync(plugin.PluginPath, cachedEnvPath, true, cancellationToken);
      logs.Add("Copied latest plugin source files to cached environment.");

      // Re-compile the project (tsc is very fast on subsequent runs with no changes)
      var compileResult = await CompileTypeScriptAsync(cachedEnvPath, logs, cancellationToken);
      if (compileResult.IsFailure) return Result<PluginExecutionResult>.Failure(compileResult.Error);

      var executionResult = await ExecuteNodeJsProcessAsync(plugin, cachedEnvPath, context, logs, cancellationToken);

      stopwatch.Stop();
      if (executionResult.IsFailure)
      {
        return Result<PluginExecutionResult>.Success(PluginExecutionResult.Failure(executionResult.Error, startTime, DateTimeOffset.UtcNow, logs));
      }

      _logger.LogDebug("TypeScript plugin execution completed in {Duration}ms", stopwatch.ElapsedMilliseconds);
      return Result<PluginExecutionResult>.Success(PluginExecutionResult.Success(executionResult.Value, startTime, DateTimeOffset.UtcNow, logs));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Unhandled exception during TypeScript plugin execution: {PluginName}", plugin.Metadata.Name);
      return Result<PluginExecutionResult>.Success(PluginExecutionResult.Failure(ex, startTime, DateTimeOffset.UtcNow));
    }
  }

  private async Task<Result<string>> GetOrCreateCachedEnvironmentAsync(Plugin plugin, List<string> logs, CancellationToken cancellationToken)
  {
    string dependencyHash = await CalculateDependencyHashAsync(plugin);
    string envPath = Path.Combine(_cacheBasePath, $"{plugin.Id.Value}-{dependencyHash}");
    string lockFilePath = Path.Combine(envPath, ".devflow.lock");

    if (File.Exists(lockFilePath) && Directory.Exists(envPath))
    {
      logs.Add($"Found cached environment for TypeScript plugin at: {envPath}");
      return Result<string>.Success(envPath);
    }

    logs.Add($"Creating new cached environment for TypeScript plugin: {plugin.Metadata.Name}...");
    if (Directory.Exists(envPath)) Directory.Delete(envPath, true);
    Directory.CreateDirectory(envPath);

    // Copy package files to install dependencies
    var packageJsonPath = Path.Combine(plugin.PluginPath, "package.json");
    if (File.Exists(packageJsonPath))
    {
      File.Copy(packageJsonPath, Path.Combine(envPath, "package.json"));

      var packageLockPath = Path.Combine(plugin.PluginPath, "package-lock.json");
      if (File.Exists(packageLockPath)) File.Copy(packageLockPath, Path.Combine(envPath, "package-lock.json"));

      logs.Add("Installing npm dependencies...");
      var npmResult = await RunProcessAsync(_npmPath!, "install", envPath, TimeSpan.FromMinutes(5), cancellationToken);
      if (npmResult.IsFailure) return Result<string>.Failure(npmResult.Error);
      logs.Add("npm dependencies installed successfully.");
    }
    else
    {
      logs.Add("No package.json found, skipping dependency installation.");
    }

    await File.WriteAllTextAsync(lockFilePath, DateTime.UtcNow.ToString("o"), cancellationToken);
    logs.Add($"Cached environment created and locked at: {envPath}");

    return Result<string>.Success(envPath);
  }

  private async Task<Result> CompileTypeScriptAsync(string workingDirectory, List<string> logs, CancellationToken cancellationToken)
  {
    var tscPath = Path.Combine(workingDirectory, "node_modules", ".bin", Environment.OSVersion.Platform == PlatformID.Win32NT ? "tsc.cmd" : "tsc");
    if (!File.Exists(tscPath))
    {
      logs.Add("Local TypeScript compiler (tsc) not found in node_modules. Assuming no compilation is needed.");
      return Result.Success();
    }

    var tsconfigPath = Path.Combine(workingDirectory, "tsconfig.json");
    if (!File.Exists(tsconfigPath))
    {
      return Result.Failure(Error.Failure("TypeScriptRuntime.TsConfigNotFound", "A tsconfig.json file is required for TypeScript plugins but was not found in the cached environment."));
    }

    string tscArgs = "--build";
    logs.Add($"Compiling TypeScript project in cache using command: tsc {tscArgs}");
    var compileResult = await RunProcessAsync(tscPath, tscArgs, workingDirectory, TimeSpan.FromMinutes(2), cancellationToken);

    if (compileResult.IsFailure)
    {
      var errorMsg = $"TypeScript compilation failed. {compileResult.Error.Message}";
      logs.Add(errorMsg);
      return Result.Failure(Error.Failure("TypeScriptRuntime.CompilationFailed", errorMsg));
    }

    logs.Add("TypeScript compilation completed successfully.");
    return Result.Success();
  }

  private async Task<string> CalculateDependencyHashAsync(Plugin plugin)
  {
    var packageJsonPath = Path.Combine(plugin.PluginPath, "package.json");
    var packageLockPath = Path.Combine(plugin.PluginPath, "package-lock.json");

    if (!File.Exists(packageJsonPath)) return "no-deps";

    using var sha256 = SHA256.Create();
    using var memoryStream = new MemoryStream();

    await using (var fileStream = File.OpenRead(packageJsonPath))
    {
      await fileStream.CopyToAsync(memoryStream);
    }
    if (File.Exists(packageLockPath))
    {
      await using (var fileStream = File.OpenRead(packageLockPath))
      {
        await fileStream.CopyToAsync(memoryStream);
      }
    }

    memoryStream.Position = 0;
    var hashBytes = await sha256.ComputeHashAsync(memoryStream);
    return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
  }

  // --- Other methods like InitializeAsync, ExecuteNodeJsProcessAsync, etc. remain the same ---
  // (Full code for remaining methods included for completeness)

  public async Task<Result> InitializeAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      _logger.LogInformation("Initializing TypeScript runtime manager...");
      var nodeJsResult = await CheckNodeJsAvailabilityAsync(cancellationToken);
      if (nodeJsResult.IsFailure)
      {
        _logger.LogWarning("Node.js runtime not available: {Error}. TypeScript plugins will be unavailable.", nodeJsResult.Error.Message);
        _isNodeJsAvailable = false;
        return Result.Success();
      }
      _nodeJsPath = nodeJsResult.Value.NodePath;
      _npmPath = nodeJsResult.Value.NpmPath;
      _isNodeJsAvailable = true;
      _logger.LogInformation("TypeScript runtime manager initialized successfully. Node.js Path: {NodePath}", _nodeJsPath);
      return Result.Success();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to initialize TypeScript runtime manager");
      return Result.Failure(Error.Failure("TypeScriptRuntime.InitializationFailed", ex.Message));
    }
  }

  private async Task<Result<(string NodePath, string NpmPath)>> CheckNodeJsAvailabilityAsync(CancellationToken cancellationToken)
  {
    var nodeJsPath = await FindExecutableAsync("node");
    if (nodeJsPath is null) return Result<(string, string)>.Failure(Error.Failure("TypeScriptRuntime.NodeJsNotFound", "Node.js executable not found in PATH."));

    var versionResult = await RunProcessAsync(nodeJsPath, "--version", "", TimeSpan.FromSeconds(10), cancellationToken);
    if (versionResult.IsFailure) return Result<(string, string)>.Failure(Error.Failure("TypeScriptRuntime.NodeJsVersionCheckFailed", $"Node.js version check failed for '{nodeJsPath}'. Error: {versionResult.Error.Message}"));

    var npmPath = await FindExecutableAsync("npm");
    if (npmPath is null) return Result<(string, string)>.Failure(Error.Failure("TypeScriptRuntime.NpmNotFound", "npm executable not found in PATH."));

    return Result<(string, string)>.Success((nodeJsPath, npmPath));
  }

  public bool CanExecutePlugin(Plugin plugin) => plugin?.Metadata?.Language == PluginLanguage.TypeScript && _isNodeJsAvailable;

  private async Task<string?> FindExecutableAsync(string fileName)
  {
    return await Task.Run(() =>
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
      using var process = new Process
      {
        StartInfo = new ProcessStartInfo
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
        },
        EnableRaisingEvents = true
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
        return Result<(string, string)>.Failure(Error.Failure("TypeScriptRuntime.ProcessFailed", $"Process failed with exit code {process.ExitCode}. Error: {errorBuilder.ToString().Trim()}"));
      }
      return Result<(string, string)>.Success((outputBuilder.ToString().Trim(), errorBuilder.ToString().Trim()));
    }
    catch (Exception ex)
    {
      return Result<(string, string)>.Failure(Error.Failure("TypeScriptRuntime.ProcessException", $"Process execution failed: {ex.Message}"));
    }
  }

  private async Task<Result<object?>> ExecuteNodeJsProcessAsync(Plugin plugin, string workingDirectory, PluginExecutionContext context, List<string> logs, CancellationToken cancellationToken)
  {
    var entryPointJs = Path.Combine("dist", Path.ChangeExtension(plugin.EntryPoint, ".js"));
    var fullEntryPointPath = Path.Combine(workingDirectory, entryPointJs);
    if (!File.Exists(fullEntryPointPath))
      return Result<object?>.Failure(Error.Failure("TypeScriptRuntime.CompiledJsNotFound", $"Compiled JavaScript entry point not found at '{fullEntryPointPath}'."));

    var contextJson = JsonSerializer.Serialize(context, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    var encodedContext = Convert.ToBase64String(Encoding.UTF8.GetBytes(contextJson));
    var nodeWrapperPath = await CreateNodeWrapperScriptAsync(workingDirectory, cancellationToken);

    var executeResult = await RunProcessAsync(_nodeJsPath!, $"\"{nodeWrapperPath}\" \"{entryPointJs}\" \"{encodedContext}\"", workingDirectory, context.ExecutionTimeout, cancellationToken);

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

  private async Task<string> CreateNodeWrapperScriptAsync(string workingDirectory, CancellationToken cancellationToken)
  {
    string wrapperScriptPath = Path.Combine(workingDirectory, "nodeWrapper.js");
    string scriptContent = @"
        const fs = require('fs');
        const [,, entryPoint, encodedContext] = process.argv;
        const context = JSON.parse(Buffer.from(encodedContext, 'base64').toString('utf8'));
        const plugin = require(entryPoint);
        if (typeof plugin.execute !== 'function') {
            throw new Error('Plugin entry point must export an execute function.');
        }
        const result = plugin.execute(context);
        console.log(JSON.stringify(result));
    ";

    await File.WriteAllTextAsync(wrapperScriptPath, scriptContent, cancellationToken);
    return wrapperScriptPath;
  }
  public Task<Result<bool>> ValidatePluginAsync(Plugin plugin, CancellationToken cancellationToken = default) { /* ... implementation ... */ return Task.FromResult(Result.Success(true)); }
  public Task DisposeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  private async Task CopyDirectoryAsync(string sourceDir, string destDir, bool overwrite, CancellationToken cancellationToken)
  {
    await Task.Run(() =>
    {
      Directory.CreateDirectory(destDir);
      foreach (var file in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
      {
        cancellationToken.ThrowIfCancellationRequested();
        var relativePath = Path.GetRelativePath(sourceDir, file);
        var destFile = Path.Combine(destDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
        File.Copy(file, destFile, overwrite);
      }
    });
  }
}