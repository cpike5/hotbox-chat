using HotBox.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotBox.Infrastructure.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedOnAdd();

        builder.Property(n => n.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(n => n.RecipientId).IsRequired();
        builder.Property(n => n.SenderId).IsRequired();

        builder.Property(n => n.PayloadJson)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(n => n.SourceId).IsRequired();

        builder.Property(n => n.SourceType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(n => n.CreatedAtUtc).IsRequired();

        builder.HasOne(n => n.Recipient)
            .WithMany()
            .HasForeignKey(n => n.RecipientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(n => n.Sender)
            .WithMany()
            .HasForeignKey(n => n.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(n => new { n.RecipientId, n.CreatedAtUtc })
            .IsDescending(false, true);

        builder.HasIndex(n => new { n.RecipientId, n.ReadAtUtc });
    }
}
