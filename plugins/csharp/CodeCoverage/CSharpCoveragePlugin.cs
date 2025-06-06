using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

/// <summary>
/// A DevFlow plugin to calculate code coverage for .NET projects using 'dotnet test' and Coverlet.
/// It executes tests, finds the generated Cobertura report, parses it, and returns a structured summary.
/// </summary>
public class CSharpCoveragePlugin
{
  private readonly List<string> _logs = new();

  /// <summary>
  /// Asynchronous plugin execution method called by the DevFlow runtime.
  /// </summary>
  /// <param name="context">Plugin execution context containing configuration and input data.</param>
  /// <param name="cancellationToken">Cancellation token for the operation.</param>
  /// <returns>A structured result with coverage summary and detailed file-level data.</returns>
  public async Task<object> ExecuteAsync(object context, CancellationToken cancellationToken = default)
  {
    var startTime = DateTime.UtcNow;
    _logs.Add("CSharpCoveragePlugin execution started.");

    try
    {
      // 1. Get configuration from the context object passed by the runtime
      var projectPath = GetConfigValue(context, "projectPath", ".");
      var reportFormat = GetConfigValue(context, "reportFormat", "cobertura");
      var workingDirectory = GetWorkingDirectory(context);
      var fullProjectPath = Path.GetFullPath(Path.Combine(workingDirectory, projectPath));

      if (!File.Exists(fullProjectPath) && !Directory.Exists(fullProjectPath))
      {
        throw new FileNotFoundException($"Project or solution file not found at '{fullProjectPath}'.");
      }

      _logs.Add($"Analyzing project: {fullProjectPath}");

      // 2. Execute 'dotnet test' with code coverage collection
      var testResult = await RunDotnetTestAsync(fullProjectPath, cancellationToken);
      if (!testResult.Success)
      {
        throw new InvalidOperationException($"'dotnet test' command failed. Error: {testResult.Error}");
      }

      // 3. Find the generated coverage report
      var reportPath = FindCoverageReport(testResult.OutputDirectory, reportFormat);
      if (string.IsNullOrEmpty(reportPath))
      {
        throw new FileNotFoundException("Could not find the coverage report file. Ensure 'coverlet.collector' is referenced in your test projects.");
      }

      _logs.Add($"Coverage report found at: {reportPath}");

      // 4. Parse the report and generate a summary
      var coverageResult = ParseCoberturaReport(reportPath);

      var executionTime = DateTime.UtcNow - startTime;
      _logs.Add($"Execution completed in {executionTime.TotalMilliseconds:F2}ms");

      return new
      {
        Success = true,
        Message = $"Coverage analysis complete. Total line coverage: {coverageResult.TotalLineRate * 100:F2}%.",
        Data = coverageResult,
        Logs = _logs
      };
    }
    catch (Exception ex)
    {
      _logs.Add($"ERROR: {ex.Message}");
      return new
      {
        Success = false,
        Error = ex.Message,
        ErrorType = ex.GetType().Name,
        StackTrace = ex.StackTrace,
        Logs = _logs
      };
    }
  }

  /// <summary>
  /// Runs the 'dotnet test' command with arguments to collect code coverage.
  /// </summary>
  private async Task<(bool Success, string OutputDirectory, string? Error)> RunDotnetTestAsync(string projectPath, CancellationToken cancellationToken)
  {
    var tempResultsDir = Path.Combine(Path.GetTempPath(), "DevFlowCoverage", Guid.NewGuid().ToString());
    Directory.CreateDirectory(tempResultsDir);

    _logs.Add($"Using temporary directory for test results: {tempResultsDir}");

    var processStartInfo = new ProcessStartInfo
    {
      FileName = "dotnet",
      Arguments = $"test \"{projectPath}\" --collect:\"XPlat Code Coverage\" --results-directory \"{tempResultsDir}\"",
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      CreateNoWindow = true,
      WorkingDirectory = Path.GetDirectoryName(projectPath)
    };

    using var process = new Process { StartInfo = processStartInfo };
    var stdOut = new StringWriter();
    var stdErr = new StringWriter();
    process.OutputDataReceived += (s, e) => { if (e.Data != null) stdOut.WriteLine(e.Data); };
    process.ErrorDataReceived += (s, e) => { if (e.Data != null) stdErr.WriteLine(e.Data); };

    _logs.Add($"Executing command: {processStartInfo.FileName} {processStartInfo.Arguments}");
    process.Start();
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();

    await process.WaitForExitAsync(cancellationToken);

    _logs.Add("'dotnet test' process exited with code: " + process.ExitCode);
    if (process.ExitCode != 0)
    {
      return (false, tempResultsDir, $"dotnet test exited with code {process.ExitCode}. Error: {stdErr.ToString()}");
    }

    return (true, tempResultsDir, null);
  }

  /// <summary>
  /// Searches the specified directory for the coverage report file.
  /// </summary>
  private string FindCoverageReport(string searchDirectory, string format)
  {
    // Coverlet places reports in a subdirectory named with a GUID
    var searchPattern = $"coverage.{format}.xml";
    var file = Directory.GetFiles(searchDirectory, searchPattern, SearchOption.AllDirectories).FirstOrDefault();
    return file ?? string.Empty;
  }

  /// <summary>
  /// Parses the Cobertura XML report to extract coverage metrics.
  /// </summary>
  private CoverageResult ParseCoberturaReport(string reportPath)
  {
    var doc = XDocument.Load(reportPath);
    var coverageElement = doc.Element("coverage");
    if (coverageElement == null) throw new InvalidDataException("Invalid Cobertura report: Missing 'coverage' root element.");

    var result = new CoverageResult
    {
      TotalLineRate = decimal.Parse(coverageElement.Attribute("line-rate")?.Value ?? "0", CultureInfo.InvariantCulture),
      TotalBranchRate = decimal.Parse(coverageElement.Attribute("branch-rate")?.Value ?? "0", CultureInfo.InvariantCulture),
      Modules = coverageElement.Descendants("package").Select(pkg => new CoverageModule
      {
        Name = pkg.Attribute("name")?.Value ?? "Unknown",
        LineRate = decimal.Parse(pkg.Attribute("line-rate")?.Value ?? "0", CultureInfo.InvariantCulture),
        Files = pkg.Descendants("class").Select(cls => new CoverageFile
        {
          Name = cls.Attribute("filename")?.Value ?? "Unknown",
          ClassName = cls.Attribute("name")?.Value ?? "Unknown",
          LineRate = decimal.Parse(cls.Attribute("line-rate")?.Value ?? "0", CultureInfo.InvariantCulture),
          UncoveredLines = cls.Descendants("line")
                               .Where(l => l.Attribute("hits")?.Value == "0")
                               .Select(l => int.Parse(l.Attribute("number")?.Value ?? "0"))
                               .ToList()
        }).ToList()
      }).ToList()
    };
    _logs.Add($"Parsed report. Total coverage: {result.TotalLineRate * 100:F2}%.");
    return result;
  }

  #region Context Helper Methods
  private static string GetConfigValue(object context, string key, string defaultValue)
  {
    var config = GetProperty<IDictionary<string, object>>(context, "Configuration");
    return config?.TryGetValue(key, out var value) == true ? value?.ToString() ?? defaultValue : defaultValue;
  }

  private static string GetWorkingDirectory(object context)
  {
    return GetProperty<string>(context, "WorkingDirectory") ?? Directory.GetCurrentDirectory();
  }

  private static T? GetProperty<T>(object? context, string key) where T : class
  {
    return context?.GetType().GetProperty(key)?.GetValue(context) as T;
  }
  #endregion

  #region DTOs for structured result
  public class CoverageResult
  {
    public decimal TotalLineRate { get; set; }
    public decimal TotalBranchRate { get; set; }
    public List<CoverageModule> Modules { get; set; } = new();
  }

  public class CoverageModule
  {
    public string Name { get; set; } = "";
    public decimal LineRate { get; set; }
    public List<CoverageFile> Files { get; set; } = new();
  }

  public class CoverageFile
  {
    public string Name { get; set; } = "";
    public string ClassName { get; set; } = "";
    public decimal LineRate { get; set; }
    public List<int> UncoveredLines { get; set; } = new();
  }
  #endregion
}