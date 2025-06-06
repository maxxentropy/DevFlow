using DevFlow.Application.Extensions;
using DevFlow.Infrastructure.Extensions;
using DevFlow.Infrastructure.Configuration;
using DevFlow.Infrastructure.Services;
using DevFlow.Presentation.MCP.Extensions;
using DevFlow.Presentation.MCP.Protocol;
using DevFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using Microsoft.Extensions.Options;
using Serilog;
using System.Text.Json;


// Add these more specific debug lines for file access
Console.WriteLine("---- FILE ACCESS DEBUG START ----");
string pythonPathToTest = @"C:\Python312\python.exe";
Console.WriteLine($"[DEBUG] Testing direct access to: {pythonPathToTest}");

try
{
  bool fileExists = System.IO.File.Exists(pythonPathToTest);
  Console.WriteLine($"[DEBUG] Result of File.Exists(): {fileExists}");

  if (fileExists)
  {
    var attributes = System.IO.File.GetAttributes(pythonPathToTest);
    Console.WriteLine($"[DEBUG] Successfully read file attributes: {attributes}");
    Console.WriteLine("[INFO] This test PASSED. The application can see and access the file.");
  }
  else
  {
    Console.WriteLine("[ERROR] File.Exists() returned false. This confirms a file access or permission problem.");
  }
}
catch (Exception ex)
{
  Console.WriteLine($"[ERROR] An exception occurred while accessing the file: {ex.Message}");
}
Console.WriteLine("---- FILE ACCESS DEBUG END ----\n");

// Create a minimal bootstrap logger for startup
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

try
{
  Log.Information("Starting DevFlow MCP Server");

  // Add configuration
  builder.Configuration
      .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
      .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
      .AddEnvironmentVariables("DEVFLOW_")
      .AddCommandLine(args);

  // Configure Serilog using settings from appsettings.json
  Log.Logger = new LoggerConfiguration()
      .ReadFrom.Configuration(builder.Configuration)
      .Enrich.FromLogContext()
      .CreateLogger();

  builder.Host.UseSerilog();

  // Add application layers
  builder.Services.AddApplication();
  builder.Services.AddInfrastructure(builder.Configuration);
  builder.Services.AddMcpServer();
  builder.Services.AddPluginSystem();

  // Add ASP.NET Core services
  builder.Services.AddControllers()
      .AddJsonOptions(options =>
      {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = builder.Environment.IsDevelopment();
      });

  builder.Services.AddEndpointsApiExplorer();
  builder.Services.AddSwaggerGen();

  // Add CORS
  builder.Services.AddCors(options =>
  {
    options.AddDefaultPolicy(policy =>
    {
      if (builder.Environment.IsDevelopment())
      {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
      }
    });
  });

  // Add health checks - CORRECTED METHOD NAME
  builder.Services.AddHealthChecks()
      .AddDbContextCheck<DevFlowDbContext>();

  var app = builder.Build();

  // Configure pipeline
  if (app.Environment.IsDevelopment())
  {
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
  }

  app.UseRouting();
  app.UseCors();

  // Add MCP protocol middleware
  app.UseMiddleware<McpProtocolMiddleware>();

  // MCP endpoint
  app.MapPost("/mcp", async (HttpContext context, McpServer mcpServer) =>
  {
    using var reader = new StreamReader(context.Request.Body);
    var requestJson = await reader.ReadToEndAsync();

    var responseJson = await mcpServer.ProcessRequestAsync(requestJson, context.RequestAborted);

    context.Response.ContentType = "application/json";
    await context.Response.WriteAsync(responseJson);
  });

  // Health checks
  app.MapHealthChecks("/health");

  // Initialize database
  using (var scope = app.Services.CreateScope())
  {
    var dbInitializer = scope.ServiceProvider.GetRequiredService<IDbInitializer>();
    await dbInitializer.InitializeAsync();
    Log.Information("Database initialized successfully");
  }

  // Set up application lifetime handling
  var appLifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
  var waitForStop = new ManualResetEventSlim(false);

  appLifetime.ApplicationStopping.Register(() => 
  {
    Log.Information("DevFlow MCP Server is shutting down...");
    waitForStop.Set();
  });

  Log.Information("DevFlow MCP Server started successfully");
  Log.Information("Press Ctrl+C to shut down");

  // Start the application
  await app.StartAsync();

  // Wait for the application to be stopped
  waitForStop.Wait();

  // Perform graceful shutdown
  await app.StopAsync();
  Log.Information("DevFlow MCP Server has shut down gracefully");
}
catch (Exception ex)
{
  Log.Fatal(ex, "DevFlow MCP Server failed to start");
  throw;
}
finally
{
  Log.CloseAndFlush();
}
