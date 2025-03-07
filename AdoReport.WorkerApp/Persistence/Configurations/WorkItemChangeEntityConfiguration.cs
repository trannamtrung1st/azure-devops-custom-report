using AdoReport.WorkerApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdoReport.WorkerApp.Persistence.Configurations;

public class WorkItemChangeEntityConfiguration : IEntityTypeConfiguration<WorkItemChangeEntity>
{
    public void Configure(EntityTypeBuilder<WorkItemChangeEntity> builder)
    {
        builder.ToTable("WorkItemChanges");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.WorkItemId)
            .IsRequired();

        builder.Property(e => e.ChangedDate)
            .IsRequired();

        builder.Property(e => e.ChangedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.BeforeFields)
            .HasColumnType("jsonb");

        builder.Property(e => e.AfterFields)
            .HasColumnType("jsonb");

        // Create indexes
        builder.HasIndex(e => e.WorkItemId);
        builder.HasIndex(e => e.ChangedDate);
        builder.HasIndex(e => e.ChangedBy);

        // Create GIN indexes for JSONB columns
        builder.HasIndex(e => e.BeforeFields)
            .HasMethod("gin");
        builder.HasIndex(e => e.AfterFields)
            .HasMethod("gin");

        // Configure relationship
        builder.HasOne(e => e.WorkItem)
            .WithMany()
            .HasForeignKey(e => e.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
