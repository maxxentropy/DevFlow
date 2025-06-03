using DevFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DevFlow.Infrastructure.Services;

/// <summary>
/// Interface for database initialization.
/// </summary>
public interface IDbInitializer
{
  /// <summary>
  /// Initializes the database schema and seeds initial data.
  /// </summary>
  Task InitializeAsync();
}

/// <summary>
/// Service for initializing the database.
/// </summary>
public sealed class DbInitializer : IDbInitializer
{
  private readonly DevFlowDbContext _context;
  private readonly ILogger<DbInitializer> _logger;

  public DbInitializer(DevFlowDbContext context, ILogger<DbInitializer> logger)
  {
    _context = context;
    _logger = logger;
  }

  public async Task InitializeAsync()
  {
    try
    {
      _logger.LogInformation("Initializing database...");

      // Ensure database is created
      await _context.Database.EnsureCreatedAsync();

      // Check if migration is needed
      var pendingMigrations = await _context.Database.GetPendingMigrationsAsync();
      if (pendingMigrations.Any())
      {
        _logger.LogInformation("Applying {Count} pending migrations", pendingMigrations.Count());
        await _context.Database.MigrateAsync();
      }

      _logger.LogInformation("Database initialization completed successfully");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to initialize database");
      throw;
    }
  }
}