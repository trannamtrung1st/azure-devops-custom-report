using AdoReport.WorkerApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdoReport.WorkerApp.Data.Configurations;

public class WorkItemFieldEntityConfiguration : IEntityTypeConfiguration<WorkItemFieldEntity>
{
    public void Configure(EntityTypeBuilder<WorkItemFieldEntity> builder)
    {
        builder.ToTable("WorkItemFields");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.FieldName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.FieldData)
            .HasColumnType("jsonb");

        // Create indexes
        builder.HasIndex(e => e.FieldName)
            .HasDatabaseName("IX_WorkItemFields_FieldName");

        builder.HasIndex(e => new { e.WorkItemId, e.FieldName })
            .IsUnique()
            .HasDatabaseName("IX_WorkItemFields_WorkItemId_FieldName");

        // Create index on the JSONB value for specific field types
        builder.HasIndex(e => e.FieldData)
            .HasMethod("gin")
            .HasDatabaseName("IX_WorkItemFields_FieldData");

        builder.HasOne(e => e.WorkItem)
            .WithMany(e => e.Fields)
            .HasForeignKey(e => e.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
