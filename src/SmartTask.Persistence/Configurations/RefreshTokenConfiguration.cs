using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTask.Domain.Constants;
using SmartTask.Domain.Entities;

namespace SmartTask.Persistence.Configurations;

/// <remarks>The relationship to <see cref="User"/> (cascade delete) is configured in <see cref="UserConfiguration"/>.</remarks>
internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.TokenHash).IsRequired().HasMaxLength(RefreshTokenConstraints.TokenHashMaxLength);
        builder.Property(t => t.ReplacedByTokenHash).HasMaxLength(RefreshTokenConstraints.TokenHashMaxLength);
        builder.Property(t => t.CreatedByIp).HasMaxLength(RefreshTokenConstraints.CreatedByIpMaxLength);
        builder.Property(t => t.RevokedReason).HasMaxLength(RefreshTokenConstraints.RevokedReasonMaxLength);

        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.ExpiresAt);

        builder.Ignore(t => t.IsRevoked);
    }
}
