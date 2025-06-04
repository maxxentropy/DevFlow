using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// HelloWorld plugin for DevFlow - demonstrates basic plugin capabilities.
/// This plugin showcases configuration usage, file operations, and structured results.
/// </summary>
public class HelloWorldPlugin
{
    /// <summary>
    /// Asynchronous plugin execution method called by the DevFlow runtime.
    /// This is the preferred execution method for I/O operations.
    /// </summary>
    /// <param name="context">Plugin execution context with configuration and input data</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Plugin execution result</returns>
    public async Task<object> ExecuteAsync(object context, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var logs = new List<string>();
        
        try
        {
            logs.Add("HelloWorld plugin execution started");
            
            // Extract configuration values with defaults
            var greeting = GetConfigValue(context, "greeting", "Hello");
            var outputPath = GetConfigValue(context, "outputPath", "./hello-output.txt");
            var includeTimestamp = GetConfigValue(context, "includeTimestamp", "true").ToLower() == "true";
            var logLevel = GetConfigValue(context, "logLevel", "info");
            
            // Get working directory and input data
            var workingDirectory = GetWorkingDirectory(context);
            var inputData = GetInputData(context);
            
            logs.Add($"Configuration loaded - Greeting: {greeting}, OutputPath: {outputPath}");
            logs.Add($"Working directory: {workingDirectory}");
            logs.Add($"Input data: {inputData ?? "null"}");
            
            // Create the greeting message
            var timestamp = includeTimestamp ? DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC") : null;
            var message = CreateGreetingMessage(greeting, inputData, timestamp);
            
            // Prepare output content
            var outputContent = new
            {
                Message = message,
                Timestamp = timestamp,
                ExecutedBy = "HelloWorldPlugin v1.0.0",
                InputReceived = inputData,
                Configuration = new
                {
                    Greeting = greeting,
                    OutputPath = outputPath,
                    IncludeTimestamp = includeTimestamp,
                    LogLevel = logLevel
                },
                WorkingDirectory = workingDirectory
            };
            
            // Write to output file
            var fullOutputPath = Path.Combine(workingDirectory, outputPath);
            var jsonContent = JsonSerializer.Serialize(outputContent, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            // Ensure directory exists
            var outputDirectory = Path.GetDirectoryName(fullOutputPath);
            if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
                logs.Add($"Created output directory: {outputDirectory}");
            }
            
            await File.WriteAllTextAsync(fullOutputPath, jsonContent, cancellationToken);
            logs.Add($"Output written to file: {fullOutputPath}");
            
            // Verify file was created
            var fileInfo = new FileInfo(fullOutputPath);
            logs.Add($"File size: {fileInfo.Length} bytes");
            
            var executionTime = DateTime.UtcNow - startTime;
            logs.Add($"Execution completed in {executionTime.TotalMilliseconds:F2}ms");
            
            // Return success result
            return new
            {
                Success = true,
                Message = message,
                OutputFile = fullOutputPath,
                FileSize = fileInfo.Length,
                ExecutionTimeMs = executionTime.TotalMilliseconds,
                Timestamp = timestamp,
                Logs = logs,
                Data = outputContent
            };
        }
        catch (OperationCanceledException)
        {
            logs.Add("Plugin execution was cancelled");
            return new
            {
                Success = false,
                Error = "Operation was cancelled",
                Logs = logs,
                Cancelled = true
            };
        }
        catch (Exception ex)
        {
            logs.Add($"Error occurred: {ex.Message}");
            return new
            {
                Success = false,
                Error = ex.Message,
                ErrorType = ex.GetType().Name,
                StackTrace = ex.StackTrace,
                Logs = logs
            };
        }
    }
    
    /// <summary>
    /// Synchronous execution method for simple operations.
    /// Falls back to async execution internally.
    /// </summary>
    /// <param name="context">Plugin execution context</param>
    /// <returns>Plugin execution result</returns>
    public object Execute(object context)
    {
        try
        {
            return ExecuteAsync(context, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            return new
            {
                Success = false,
                Error = ex.Message,
                ErrorType = ex.GetType().Name,
                ExecutionMethod = "Synchronous"
            };
        }
    }
    
    /// <summary>
    /// Helper method to safely extract configuration values.
    /// </summary>
    /// <param name="context">Plugin execution context</param>
    /// <param name="key">Configuration key</param>
    /// <param name="defaultValue">Default value if key not found</param>
    /// <returns>Configuration value or default</returns>
    private static string GetConfigValue(object context, string key, string defaultValue)
    {
        try
        {
            // Use reflection to get Configuration property
            var configProperty = context?.GetType().GetProperty("Configuration");
            if (configProperty != null)
            {
                var configuration = configProperty.GetValue(context);
                if (configuration != null)
                {
                    var config = configuration as IDictionary<string, object>;
                    if (config?.ContainsKey(key) == true)
                    {
                        return config[key]?.ToString() ?? defaultValue;
                    }
                    
                    // Try reflection-based property access for anonymous objects
                    try
                    {
                        var configType = configuration.GetType();
                        var property = configType.GetProperty(key);
                        if (property != null)
                        {
                            var value = property.GetValue(configuration);
                            return value?.ToString() ?? defaultValue;
                        }
                    }
                    catch
                    {
                        // Ignore reflection errors and use default
                    }
                }
            }
            return defaultValue;
        }
        catch (Exception)
        {
            return defaultValue;
        }
    }
    
    /// <summary>
    /// Gets the working directory from the context.
    /// </summary>
    /// <param name="context">Plugin execution context</param>
    /// <returns>Working directory path</returns>
    private static string GetWorkingDirectory(object context)
    {
        try
        {
            var workingDirProperty = context?.GetType().GetProperty("WorkingDirectory");
            if (workingDirProperty != null)
            {
                var workingDir = workingDirProperty.GetValue(context);
                return workingDir?.ToString() ?? Environment.CurrentDirectory;
            }
            return Environment.CurrentDirectory;
        }
        catch (Exception)
        {
            return Environment.CurrentDirectory;
        }
    }
    
    /// <summary>
    /// Gets the input data from the context.
    /// </summary>
    /// <param name="context">Plugin execution context</param>
    /// <returns>Input data as string</returns>
    private static string? GetInputData(object context)
    {
        try
        {
            var inputDataProperty = context?.GetType().GetProperty("InputData");
            if (inputDataProperty == null)
                return null;
                
            var inputData = inputDataProperty.GetValue(context);
            if (inputData == null)
                return null;
            
            if (inputData is string str)
                return str;
                
            if (inputData.GetType().Name == "JsonElement")
            {
                // Handle JsonElement without direct type reference
                try
                {
                    var valueKindProperty = inputData.GetType().GetProperty("ValueKind");
                    if (valueKindProperty != null)
                    {
                        var valueKind = valueKindProperty.GetValue(inputData);
                        if (valueKind?.ToString() == "String")
                        {
                            var getStringMethod = inputData.GetType().GetMethod("GetString", new Type[0]);
                            return getStringMethod?.Invoke(inputData, null)?.ToString();
                        }
                    }
                    return inputData.ToString();
                }
                catch
                {
                    return inputData.ToString();
                }
            }
            
            // For complex objects, serialize to JSON
            try
            {
                return JsonSerializer.Serialize(inputData);
            }
            catch
            {
                // Fallback to ToString if serialization fails
                return inputData?.ToString() ?? "World";
            }
        }
        catch (Exception)
        {
            return "World"; // Default fallback
        }
    }
    
    /// <summary>
    /// Creates a formatted greeting message.
    /// </summary>
    /// <param name="greeting">Greeting prefix</param>
    /// <param name="target">Target of the greeting</param>
    /// <param name="timestamp">Optional timestamp</param>
    /// <returns>Formatted greeting message</returns>
    private static string CreateGreetingMessage(string greeting, string? target, string? timestamp)
    {
        var targetText = string.IsNullOrWhiteSpace(target) ? "World" : target;
        var message = $"{greeting}, {targetText}! Welcome to DevFlow Plugin System.";
        
        if (!string.IsNullOrWhiteSpace(timestamp))
        {
            message += $" [{timestamp}]";
        }
        
        return message;
    }
}