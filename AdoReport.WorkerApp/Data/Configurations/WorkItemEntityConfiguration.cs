using AdoReport.WorkerApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdoReport.WorkerApp.Data.Configurations;

public class WorkItemEntityConfiguration : IEntityTypeConfiguration<WorkItemEntity>
{
    public void Configure(EntityTypeBuilder<WorkItemEntity> builder)
    {
        builder.ToTable("WorkItems");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Type)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.State)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.AssignedTo)
            .HasColumnType("jsonb");

        builder.Property(e => e.AreaPath)
            .HasMaxLength(500);

        builder.HasIndex(e => e.Type);
        builder.HasIndex(e => e.State);
        builder.HasIndex(e => e.AreaPath);
        builder.HasIndex(e => e.ParentId);
        builder.HasIndex(e => e.CreatedDate);
        builder.HasIndex(e => e.ChangedDate);

        // Create GIN index for the entire JSONB column
        builder.HasIndex(e => e.AssignedTo)
            .HasMethod("gin");

        builder.HasMany(e => e.Fields)
            .WithOne(e => e.WorkItem)
            .HasForeignKey(e => e.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
