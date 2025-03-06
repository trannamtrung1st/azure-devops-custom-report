using AdoReport.WorkerApp.Data.Configurations;
using AdoReport.WorkerApp.Models;
using Microsoft.EntityFrameworkCore;

namespace AdoReport.WorkerApp.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<WorkItemEntity> WorkItems => Set<WorkItemEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new WorkItemEntityConfiguration());
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            return;
        }

        // Enable sensitive data logging only in development
#if DEBUG
        optionsBuilder.EnableSensitiveDataLogging();
#endif

        // Configure PostgreSQL specific options
        optionsBuilder.UseNpgsql(options =>
        {
            options.EnableRetryOnFailure(3);
            options.CommandTimeout(30);
            options.MigrationsHistoryTable("__EFMigrationsHistory", "public");
        });
    }
}
