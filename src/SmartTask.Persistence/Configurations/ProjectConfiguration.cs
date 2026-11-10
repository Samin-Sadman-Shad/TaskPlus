using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTask.Domain.Constants;
using SmartTask.Domain.Entities;

namespace SmartTask.Persistence.Configurations;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(ProjectConstraints.NameMaxLength);
        builder.Property(p => p.Description).HasMaxLength(ProjectConstraints.DescriptionMaxLength);
        builder.Property(p => p.StartDate).IsRequired();
        builder.Property(p => p.RowVersion).IsRowVersion();

        // Unique per owner among live projects only, so a deleted project's name can be reused.
        builder.HasIndex(p => new { p.OwnerId, p.Name })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasOne(p => p.Owner)
            .WithMany()
            .HasForeignKey(p => p.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Members)
            .WithOne()
            .HasForeignKey(m => m.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Tasks)
            .WithOne()
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Members).HasField("_members").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(p => p.Tasks).HasField("_tasks").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
