using CustomsBot.Data;
using Microsoft.EntityFrameworkCore;

namespace CustomsBot.MigrationService;

/// <summary>One-shot: applies pending EF migrations, then stops the host.</summary>
public class MigrationWorker(
    IServiceProvider serviceProvider,
    IHostApplicationLifetime lifetime,
    ILogger<MigrationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CustomsBotDbContext>();
            await db.Database.MigrateAsync(stoppingToken);
            logger.LogInformation("Database migrations applied.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database migration failed.");
            throw;
        }

        lifetime.StopApplication();
    }
}
