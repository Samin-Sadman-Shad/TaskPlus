using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTask.Domain.Constants;
using SmartTask.Domain.Entities;

namespace SmartTask.Persistence.Configurations;

/// <remarks>The relationship to <see cref="Project"/> is configured in <see cref="ProjectConfiguration"/>.</remarks>
internal sealed class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("TaskItems");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title).IsRequired().HasMaxLength(TaskItemConstraints.TitleMaxLength);
        builder.Property(t => t.Description).HasMaxLength(TaskItemConstraints.DescriptionMaxLength);
        builder.Property(t => t.Status).IsRequired();
        builder.Property(t => t.Priority).IsRequired();
        builder.Property(t => t.RowVersion).IsRowVersion();

        builder.HasOne(t => t.Assignee)
            .WithMany()
            .HasForeignKey(t => t.AssigneeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(t => new { t.ProjectId, t.Status });
        builder.HasIndex(t => t.AssigneeId);
        builder.HasIndex(t => t.DueDate);
    }
}
