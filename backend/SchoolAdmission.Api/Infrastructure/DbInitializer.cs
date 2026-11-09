using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;

namespace SchoolAdmission.Api.Infrastructure;

public static class DbInitializer
{
    /// <summary>
    /// Creates the schema on startup. Retries because Postgres (or a second backend
    /// process starting at the same time) may not be ready yet.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider services, ILogger logger)
    {
        const int maxAttempts = 15;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var scope = services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.EnsureCreatedAsync();
                logger.LogInformation("Database ready.");
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning("Database not ready (attempt {Attempt}/{Max}): {Message}",
                    attempt, maxAttempts, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }
    }
}
