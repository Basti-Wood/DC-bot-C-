using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DCBot.Core.Database;

/// <summary>
/// Creates the database schema at startup, retrying while the MariaDB
/// container is still booting (docker-compose healthcheck covers most of
/// this, but the retry makes local runs robust too).
/// </summary>
public static class DatabaseInitializer
{
    public static async Task EnsureCreatedAsync(IServiceProvider services)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Database");
        var factory = services.GetRequiredService<IDbContextFactory<BotDbContext>>();

        const int maxAttempts = 30;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await using var db = await factory.CreateDbContextAsync();
                await db.Database.EnsureCreatedAsync();
                logger.LogInformation("Database schema ready");
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning("Database not ready (attempt {Attempt}/{Max}): {Message}",
                    attempt, maxAttempts, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }

        throw new InvalidOperationException("Could not connect to the database after retries.");
    }
}
