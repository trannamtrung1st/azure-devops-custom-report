using AdoReport.WorkerApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdoReport.WorkerApp.Persistence.Configurations;

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

        builder.Property(e => e.AreaPath)
            .HasMaxLength(500);

        builder.Property(e => e.Fields)
            .HasColumnType("jsonb");

        builder.HasIndex(e => e.Type);
        builder.HasIndex(e => e.State);
        builder.HasIndex(e => e.AreaPath);
        builder.HasIndex(e => e.ParentId);
        builder.HasIndex(e => e.CreatedDate);
        builder.HasIndex(e => e.ChangedDate);

        // Create GIN index for the JSONB column
        builder.HasIndex(e => e.Fields)
            .HasMethod("gin");
    }
}
