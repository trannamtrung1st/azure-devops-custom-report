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
    public DbSet<WorkItemChangeEntity> WorkItemChanges => Set<WorkItemChangeEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new WorkItemEntityConfiguration());
        modelBuilder.ApplyConfiguration(new WorkItemChangeEntityConfiguration());
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
