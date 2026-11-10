using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTask.Domain.Constants;
using SmartTask.Domain.Entities;

namespace SmartTask.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.FirstName).IsRequired().HasMaxLength(UserConstraints.FirstNameMaxLength);
        builder.Property(u => u.LastName).IsRequired().HasMaxLength(UserConstraints.LastNameMaxLength);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(UserConstraints.EmailMaxLength);
        builder.Property(u => u.NormalizedEmail).IsRequired().HasMaxLength(UserConstraints.EmailMaxLength);
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.Role).IsRequired();

        builder.HasIndex(u => u.NormalizedEmail).IsUnique();

        builder.Ignore(u => u.FullName);

        builder.HasMany(u => u.RefreshTokens)
            .WithOne()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(u => u.RefreshTokens)
            .HasField("_refreshTokens")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
