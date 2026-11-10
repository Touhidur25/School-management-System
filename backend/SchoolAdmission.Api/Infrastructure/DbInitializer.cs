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
                await UpgradeSchemaAsync(db);
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

    /// <summary>
    /// EnsureCreated does nothing when the database already has tables, so a database
    /// made by an earlier version would never get tables added later. This adds them
    /// without touching existing data. On a brand-new database it changes nothing,
    /// because EnsureCreated has already created everything.
    /// </summary>
    private static async Task UpgradeSchemaAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS notifications (
                id varchar(32) NOT NULL PRIMARY KEY,
                student_id varchar(17) NOT NULL,
                hold_id varchar(32) NOT NULL REFERENCES holds (id) ON DELETE RESTRICT,
                type varchar(40) NOT NULL,
                previous_status varchar(16) NOT NULL,
                created_at timestamp with time zone NOT NULL,
                read_at timestamp with time zone NULL
            );
            CREATE INDEX IF NOT EXISTS ix_notifications_student_created
                ON notifications (student_id, created_at);
            """);
    }
}
