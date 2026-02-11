using HotBox.Core.Entities;
using HotBox.Core.Enums;
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

        builder.Property(u => u.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(u => u.LastSeenUtc)
            .IsRequired();

        builder.Property(u => u.CreatedAtUtc)
            .IsRequired();

        builder.HasMany(u => u.Messages)
            .WithOne(m => m.Author)
            .HasForeignKey(m => m.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(u => u.SentDirectMessages)
            .WithOne(dm => dm.Sender)
            .HasForeignKey(dm => dm.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(u => u.ReceivedDirectMessages)
            .WithOne(dm => dm.Recipient)
            .HasForeignKey(dm => dm.RecipientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
