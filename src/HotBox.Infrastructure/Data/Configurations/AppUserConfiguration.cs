using HotBox.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotBox.Infrastructure.Data.Configurations;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.Property(u => u.DisplayName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.AvatarUrl)
            .HasMaxLength(500);

        builder.Property(u => u.Bio)
            .HasMaxLength(500);

        builder.Property(u => u.Pronouns)
            .HasMaxLength(50);

        builder.Property(u => u.CustomStatus)
            .HasMaxLength(100);

        builder.HasIndex(u => u.IsAgent);

        builder.HasOne(u => u.CreatedByApiKey)
            .WithMany(k => k.CreatedAgents)
            .HasForeignKey(u => u.CreatedByApiKeyId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
