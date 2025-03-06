using Microsoft.EntityFrameworkCore;

namespace AdoReport.WorkerApp.Data.Extensions;

public static class MigrationExtensions
{
    public static async Task MigrateDatabaseAsync(this IHost host)
    {
        using (var scope = host.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            try
            {
                var context = services.GetRequiredService<AppDbContext>();
                var logger = services.GetRequiredService<ILogger<AppDbContext>>();

                logger.LogInformation("Starting database migration...");
                await context.Database.MigrateAsync();
                logger.LogInformation("Database migration completed successfully.");
            }
            catch (Exception ex)
            {
                var logger = services.GetRequiredService<ILogger<AppDbContext>>();
                logger.LogError(ex, "An error occurred while migrating the database.");
                throw;
            }
        }
    }
}
