using DevFlow.Application.Plugins.Runtime.Models;
using DevFlow.Domain.Common;
using DevFlow.Domain.Plugins.Entities;
using DevFlow.SharedKernel.Results;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Security;
using System.Security.Permissions;
using System.Security.Policy;
using System.Text.Json;

namespace DevFlow.Infrastructure.Plugins.Security;

/// <summary>
/// Manages security policies and sandboxing for plugin execution.
/// Provides resource limiting, permission management, and secure execution contexts.
/// </summary>
public sealed class PluginSecurityManager : IDisposable
{
  private readonly ILogger<PluginSecurityManager> _logger;
  private readonly PluginSecurityOptions _options;
  private readonly Dictionary<string, PluginSecurityContext> _activeContexts;
  private readonly object _lock = new();
  private bool _disposed;

  public PluginSecurityManager(
      ILogger<PluginSecurityManager> logger,
      PluginSecurityOptions? options = null)
  {
    _logger = logger;
    _options = options ?? PluginSecurityOptions.Default;
    _activeContexts = new Dictionary<string, PluginSecurityContext>();
  }

  /// <summary>
  /// Creates a secure execution context for the plugin with resource limits and permissions.
  /// </summary>
  public async Task<Result<PluginSecurityContext>> CreateSecureContextAsync(
      Plugin plugin,
      PluginExecutionContext executionContext,
      CancellationToken cancellationToken = default)
  {
    try
    {
      _logger.LogDebug("Creating secure context for plugin: {PluginName}", plugin.Metadata.Name);

      var contextId = Guid.NewGuid().ToString();
      var securityPolicy = await CreateSecurityPolicyAsync(plugin, executionContext, cancellationToken);

      if (securityPolicy.IsFailure)
        return Result<PluginSecurityContext>.Failure(securityPolicy.Error);

      var securityContext = new PluginSecurityContext
      {
        ContextId = contextId,
        PluginId = plugin.Id,
        SecurityPolicy = securityPolicy.Value,
        MaxExecutionTime = executionContext.ExecutionTimeout,
        MaxMemoryBytes = executionContext.MaxMemoryBytes,
        CreatedAt = DateTimeOffset.UtcNow,
        WorkingDirectory = CreateSecureWorkingDirectory(plugin, contextId),
        EnvironmentVariables = FilterEnvironmentVariables(executionContext.EnvironmentVariables)
      };

      lock (_lock)
      {
        _activeContexts[contextId] = securityContext;
      }

      _logger.LogDebug("Secure context created: {ContextId} for plugin: {PluginName}",
          contextId, plugin.Metadata.Name);

      return Result<PluginSecurityContext>.Success(securityContext);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to create secure context for plugin: {PluginName}", plugin.Metadata.Name);
      return Result<PluginSecurityContext>.Failure(Error.Failure(
          "PluginSecurity.ContextCreationFailed", $"Failed to create secure context: {ex.Message}"));
    }
  }

  /// <summary>
  /// Monitors resource usage during plugin execution and enforces limits.
  /// </summary>
  public async Task<Result> MonitorExecutionAsync(
      string contextId,
      Process? process = null,
      CancellationToken cancellationToken = default)
  {
    if (!_activeContexts.TryGetValue(contextId, out var securityContext)) // Renamed from 'context'
      return Result.Failure(Error.NotFound("PluginSecurity.ContextNotFound", $"Security context {contextId} not found"));

    try
    {
      _logger.LogDebug("Starting execution monitoring for context: {ContextId}", contextId);

      var monitoringTask = Task.Run(async () =>
      {
        var stopwatch = Stopwatch.StartNew();
        var maxMemoryUsed = 0L;

        while (!cancellationToken.IsCancellationRequested && stopwatch.Elapsed < securityContext.MaxExecutionTime)
        {
          try
          {
            // Monitor memory usage
            var currentMemory = GC.GetTotalMemory(false);
            maxMemoryUsed = Math.Max(maxMemoryUsed, currentMemory);

            if (currentMemory > securityContext.MaxMemoryBytes)
            {
              _logger.LogWarning("Plugin {PluginId} exceeded memory limit: {CurrentMemory} > {MaxMemory}",
                  securityContext.PluginId.Value, currentMemory, securityContext.MaxMemoryBytes);

              // Attempt graceful shutdown first
              if (process != null && !process.HasExited)
              {
                try
                {
                  process.Kill(entireProcessTree: true);
                }
                catch (Exception ex)
                {
                  _logger.LogWarning(ex, "Failed to kill process for context: {ContextId}", contextId);
                }
              }

              throw new SecurityException($"Plugin exceeded memory limit: {currentMemory} bytes > {securityContext.MaxMemoryBytes} bytes");
            }

            // ... rest of monitoring logic

            await Task.Delay(100, cancellationToken); // Check every 100ms
          }
          catch (OperationCanceledException)
          {
            break;
          }
        }

        securityContext.PeakMemoryUsage = maxMemoryUsed;
        securityContext.ActualExecutionTime = stopwatch.Elapsed;

        // ... rest of timeout handling logic
      }, cancellationToken);

      await monitoringTask;

      return Result.Success();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Monitoring failed for context: {ContextId}", contextId);
      return Result.Failure(Error.Failure("PluginSecurity.MonitoringFailed", $"Monitoring failed: {ex.Message}"));
    }
  }


  /// <summary>
  /// Validates that the plugin's dependencies and code meet security requirements.
  /// </summary>
  public async Task<Result<PluginSecurityAssessment>> AssessPluginSecurityAsync(
      Plugin plugin,
      CancellationToken cancellationToken = default)
  {
    try
    {
      _logger.LogDebug("Performing security assessment for plugin: {PluginName}", plugin.Metadata.Name);

      var assessment = new PluginSecurityAssessment
      {
        PluginId = plugin.Id,
        AssessmentDate = DateTimeOffset.UtcNow,
        SecurityRisks = new List<SecurityRisk>(),
        TrustLevel = SecurityTrustLevel.Unknown
      };

      // Check file system access patterns
      await AssessFileSystemAccess(plugin, assessment, cancellationToken);

      // Check network access requirements
      await AssessNetworkAccess(plugin, assessment, cancellationToken);

      // Check dependency security
      await AssessDependencySecurity(plugin, assessment, cancellationToken);

      // Check code patterns for suspicious activities
      await AssessCodePatterns(plugin, assessment, cancellationToken);

      // Determine overall trust level
      assessment.TrustLevel = CalculateTrustLevel(assessment.SecurityRisks);

      _logger.LogInformation("Security assessment completed for plugin: {PluginName}. Trust level: {TrustLevel}, Risks: {RiskCount}",
          plugin.Metadata.Name, assessment.TrustLevel, assessment.SecurityRisks.Count);

      return Result<PluginSecurityAssessment>.Success(assessment);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Security assessment failed for plugin: {PluginName}", plugin.Metadata.Name);
      return Result<PluginSecurityAssessment>.Failure(Error.Failure(
          "PluginSecurity.AssessmentFailed", $"Security assessment failed: {ex.Message}"));
    }
  }

  /// <summary>
  /// Releases a security context and cleans up associated resources.
  /// </summary>
  public async Task<Result> ReleaseContextAsync(string contextId)
  {
    try
    {
      lock (_lock)
      {
        if (!_activeContexts.TryGetValue(contextId, out var context))
          return Result.Failure(Error.NotFound("PluginSecurity.ContextNotFound", $"Security context {contextId} not found"));

        _activeContexts.Remove(contextId);
      }

      // Cleanup working directory
      if (_activeContexts.TryGetValue(contextId, out var securityContext))
      {
        await CleanupWorkingDirectoryAsync(securityContext.WorkingDirectory);
      }

      _logger.LogDebug("Security context released: {ContextId}", contextId);
      return Result.Success();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to release security context: {ContextId}", contextId);
      return Result.Failure(Error.Failure("PluginSecurity.ContextReleaseFailed", $"Failed to release context: {ex.Message}"));
    }
  }

  // Private helper methods

  private async Task<Result<PluginSecurityPolicy>> CreateSecurityPolicyAsync(
      Plugin plugin,
      PluginExecutionContext executionContext,
      CancellationToken cancellationToken)
  {
    try
    {
      await Task.Yield(); // Make this actually async

      var policy = new PluginSecurityPolicy
      {
        AllowedFileSystemPaths = GetAllowedFileSystemPaths(plugin, executionContext),
        AllowedNetworkAccess = _options.AllowNetworkAccess,
        AllowedEnvironmentVariables = _options.AllowedEnvironmentVariables.ToHashSet(),
        RestrictedAssemblies = _options.RestrictedAssemblies.ToHashSet(),
        MaxCpuTime = executionContext.ExecutionTimeout,
        MaxMemoryBytes = executionContext.MaxMemoryBytes,
        AllowReflection = _options.AllowReflection,
        AllowFileIO = _options.AllowFileIO,
        AllowRegistryAccess = false,
        AllowProcessExecution = false
      };

      return Result<PluginSecurityPolicy>.Success(policy);
    }
    catch (Exception ex)
    {
      return Result<PluginSecurityPolicy>.Failure(Error.Failure(
          "PluginSecurity.PolicyCreationFailed", $"Failed to create security policy: {ex.Message}"));
    }
  }

  private HashSet<string> GetAllowedFileSystemPaths(Plugin plugin, PluginExecutionContext executionContext)
  {
    var allowedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            plugin.PluginPath,
            executionContext.WorkingDirectory,
            Path.GetTempPath()
        };

    // Add configured allowed paths
    foreach (var path in _options.AllowedFileSystemPaths)
    {
      allowedPaths.Add(Path.GetFullPath(path));
    }

    return allowedPaths;
  }

  private string CreateSecureWorkingDirectory(Plugin plugin, string contextId)
  {
    var basePath = Path.Combine(Path.GetTempPath(), "DevFlowSecure");
    var workingDir = Path.Combine(basePath, $"{plugin.Id.Value}_{contextId}");

    Directory.CreateDirectory(workingDir);

    // Set restrictive permissions only on Windows
    try
    {
      if (OperatingSystem.IsWindows())
      {
        var dirInfo = new DirectoryInfo(workingDir);
        var security = dirInfo.GetAccessControl();
        // Additional security hardening would go here for Windows
      }
      else
      {
        // For non-Windows systems, use chmod if available
        // This would require additional P/Invoke or Process.Start calls
        _logger.LogDebug("Skipping ACL setup on non-Windows platform for: {WorkingDir}", workingDir);
      }
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to set restrictive permissions on working directory: {WorkingDir}", workingDir);
    }

    return workingDir;
  }
  private Dictionary<string, string> FilterEnvironmentVariables(IReadOnlyDictionary<string, string> environmentVariables)
  {
    var filtered = new Dictionary<string, string>();

    foreach (var kvp in environmentVariables)
    {
      if (_options.AllowedEnvironmentVariables.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase))
      {
        filtered[kvp.Key] = kvp.Value;
      }
    }

    // Add safe system variables
    var safeSystemVars = new[] { "PATH", "TEMP", "TMP", "USERPROFILE", "HOME" };
    foreach (var varName in safeSystemVars)
    {
      var value = Environment.GetEnvironmentVariable(varName);
      if (!string.IsNullOrEmpty(value))
      {
        filtered[varName] = value;
      }
    }

    return filtered;
  }

  private async Task AssessFileSystemAccess(Plugin plugin, PluginSecurityAssessment assessment, CancellationToken cancellationToken)
  {
    try
    {
      var entryPointPath = Path.Combine(plugin.PluginPath, plugin.EntryPoint);
      if (File.Exists(entryPointPath))
      {
        var content = await File.ReadAllTextAsync(entryPointPath, cancellationToken);

        // Check for suspicious file system operations
        var suspiciousPatterns = new[]
        {
                    @"File\.Delete",
                    @"Directory\.Delete",
                    @"File\.Move",
                    @"File\.Copy.*\\Windows\\",
                    @"File\.Copy.*\\Program Files\\",
                    @"Registry\.",
                    @"Process\.Start",
                    @"Assembly\.LoadFrom",
                    @"Assembly\.LoadFile"
                };

        foreach (var pattern in suspiciousPatterns)
        {
          if (System.Text.RegularExpressions.Regex.IsMatch(content, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
          {
            assessment.SecurityRisks.Add(new SecurityRisk
            {
              RiskType = SecurityRiskType.SuspiciousFileSystemAccess,
              Severity = SecurityRiskSeverity.Medium,
              Description = $"Potentially dangerous file system operation detected: {pattern}",
              Location = entryPointPath
            });
          }
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to assess file system access for plugin: {PluginName}", plugin.Metadata.Name);
    }
  }

  private async Task AssessNetworkAccess(Plugin plugin, PluginSecurityAssessment assessment, CancellationToken cancellationToken)
  {
    try
    {
      var entryPointPath = Path.Combine(plugin.PluginPath, plugin.EntryPoint);
      if (File.Exists(entryPointPath))
      {
        var content = await File.ReadAllTextAsync(entryPointPath, cancellationToken);

        // Check for network operations
        var networkPatterns = new[]
        {
                    @"HttpClient",
                    @"WebRequest",
                    @"Socket",
                    @"TcpClient",
                    @"UdpClient",
                    @"FtpWebRequest"
                };

        foreach (var pattern in networkPatterns)
        {
          if (System.Text.RegularExpressions.Regex.IsMatch(content, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
          {
            assessment.SecurityRisks.Add(new SecurityRisk
            {
              RiskType = SecurityRiskType.NetworkAccess,
              Severity = SecurityRiskSeverity.Low,
              Description = $"Network access detected: {pattern}",
              Location = entryPointPath
            });
          }
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to assess network access for plugin: {PluginName}", plugin.Metadata.Name);
    }
  }

  private async Task AssessDependencySecurity(Plugin plugin, PluginSecurityAssessment assessment, CancellationToken cancellationToken)
  {
    await Task.Run(() =>
    {
      foreach (var dependency in plugin.Dependencies)
      {
        // Check against known vulnerable packages (simplified example)
        if (_options.KnownVulnerablePackages.ContainsKey(dependency.Name))
        {
          var vulnerableVersions = _options.KnownVulnerablePackages[dependency.Name];
          // In a real implementation, you'd check if the dependency version matches vulnerable versions
          assessment.SecurityRisks.Add(new SecurityRisk
          {
            RiskType = SecurityRiskType.VulnerableDependency,
            Severity = SecurityRiskSeverity.High,
            Description = $"Dependency '{dependency.Name}' is known to have security vulnerabilities",
            Location = $"Dependency: {dependency.GetDescription()}"
          });
        }
      }
    }, cancellationToken);
  }

  private async Task AssessCodePatterns(Plugin plugin, PluginSecurityAssessment assessment, CancellationToken cancellationToken)
  {
    try
    {
      var entryPointPath = Path.Combine(plugin.PluginPath, plugin.EntryPoint);
      if (File.Exists(entryPointPath))
      {
        var content = await File.ReadAllTextAsync(entryPointPath, cancellationToken);

        // Check for dangerous reflection patterns
        var dangerousPatterns = new[]
        {
                    @"Assembly\.Load.*byte\[\]",
                    @"Activator\.CreateInstance.*Type\.GetType",
                    @"Marshal\.",
                    @"DllImport",
                    @"unsafe\s*{",
                    @"fixed\s*\(",
                    @"stackalloc"
                };

        foreach (var pattern in dangerousPatterns)
        {
          if (System.Text.RegularExpressions.Regex.IsMatch(content, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
          {
            assessment.SecurityRisks.Add(new SecurityRisk
            {
              RiskType = SecurityRiskType.DangerousCodePattern,
              Severity = SecurityRiskSeverity.High,
              Description = $"Dangerous code pattern detected: {pattern}",
              Location = entryPointPath
            });
          }
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to assess code patterns for plugin: {PluginName}", plugin.Metadata.Name);
    }
  }

  private SecurityTrustLevel CalculateTrustLevel(List<SecurityRisk> risks)
  {
    if (!risks.Any())
      return SecurityTrustLevel.High;

    var highRisks = risks.Count(r => r.Severity == SecurityRiskSeverity.High);
    var mediumRisks = risks.Count(r => r.Severity == SecurityRiskSeverity.Medium);

    if (highRisks > 0)
      return SecurityTrustLevel.Low;

    if (mediumRisks > 2)
      return SecurityTrustLevel.Low;

    if (mediumRisks > 0)
      return SecurityTrustLevel.Medium;

    return SecurityTrustLevel.High;
  }

  private async Task CleanupWorkingDirectoryAsync(string workingDirectory)
  {
    try
    {
      if (Directory.Exists(workingDirectory))
      {
        // Give some time for any lingering file handles to be released
        await Task.Delay(100);
        Directory.Delete(workingDirectory, true);
        _logger.LogDebug("Cleaned up working directory: {WorkingDirectory}", workingDirectory);
      }
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to cleanup working directory: {WorkingDirectory}", workingDirectory);
    }
  }

  public void Dispose()
  {
    if (_disposed) return;

    try
    {
      var contextIds = _activeContexts.Keys.ToList();
      foreach (var contextId in contextIds)
      {
        _ = ReleaseContextAsync(contextId).GetAwaiter().GetResult();
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error during PluginSecurityManager disposal");
    }
    finally
    {
      _disposed = true;
    }
  }
}

/// <summary>
/// Security configuration options for plugin execution.
/// </summary>
public sealed class PluginSecurityOptions
{
  public bool AllowNetworkAccess { get; set; } = false;
  public bool AllowFileIO { get; set; } = true;
  public bool AllowReflection { get; set; } = false;
  public List<string> AllowedFileSystemPaths { get; set; } = new();
  public List<string> AllowedEnvironmentVariables { get; set; } = new() { "PATH", "TEMP", "TMP" };
  public List<string> RestrictedAssemblies { get; set; } = new();
  public Dictionary<string, List<string>> KnownVulnerablePackages { get; set; } = new();

  public static PluginSecurityOptions Default => new()
  {
    AllowNetworkAccess = false,
    AllowFileIO = true,
    AllowReflection = false,
    AllowedFileSystemPaths = new List<string>(),
    AllowedEnvironmentVariables = new List<string> { "PATH", "TEMP", "TMP", "HOME", "USERPROFILE" },
    RestrictedAssemblies = new List<string> { "System.Management", "Microsoft.Win32" }
  };
}

/// <summary>
/// Security context for plugin execution.
/// </summary>
public sealed class PluginSecurityContext
{
  public required string ContextId { get; init; }
  public required PluginId PluginId { get; init; }
  public required PluginSecurityPolicy SecurityPolicy { get; init; }
  public required TimeSpan MaxExecutionTime { get; init; }
  public required long MaxMemoryBytes { get; init; }
  public required DateTimeOffset CreatedAt { get; init; }
  public required string WorkingDirectory { get; init; }
  public required Dictionary<string, string> EnvironmentVariables { get; init; }

  public long PeakMemoryUsage { get; set; }
  public TimeSpan ActualExecutionTime { get; set; }
}

/// <summary>
/// Security policy defining what a plugin is allowed to do.
/// </summary>
public sealed class PluginSecurityPolicy
{
  public required HashSet<string> AllowedFileSystemPaths { get; init; }
  public required bool AllowedNetworkAccess { get; init; }
  public required HashSet<string> AllowedEnvironmentVariables { get; init; }
  public required HashSet<string> RestrictedAssemblies { get; init; }
  public required TimeSpan MaxCpuTime { get; init; }
  public required long MaxMemoryBytes { get; init; }
  public required bool AllowReflection { get; init; }
  public required bool AllowFileIO { get; init; }
  public required bool AllowRegistryAccess { get; init; }
  public required bool AllowProcessExecution { get; init; }
}

/// <summary>
/// Security assessment results for a plugin.
/// </summary>
public sealed class PluginSecurityAssessment
{
  public required PluginId PluginId { get; init; }
  public required DateTimeOffset AssessmentDate { get; init; }
  public required List<SecurityRisk> SecurityRisks { get; init; }
  public required SecurityTrustLevel TrustLevel { get; set; }
}

/// <summary>
/// Represents a security risk identified in a plugin.
/// </summary>
public sealed class SecurityRisk
{
  public required SecurityRiskType RiskType { get; init; }
  public required SecurityRiskSeverity Severity { get; init; }
  public required string Description { get; init; }
  public required string Location { get; init; }
}

/// <summary>
/// Types of security risks that can be identified.
/// </summary>
public enum SecurityRiskType
{
  SuspiciousFileSystemAccess,
  NetworkAccess,
  VulnerableDependency,
  DangerousCodePattern,
  UnauthorizedAssemblyLoading,
  PrivilegeEscalation
}

/// <summary>
/// Severity levels for security risks.
/// </summary>
public enum SecurityRiskSeverity
{
  Low,
  Medium,
  High,
  Critical
}

/// <summary>
/// Trust levels for plugin security.
/// </summary>
public enum SecurityTrustLevel
{
  Unknown,
  Low,
  Medium,
  High
}