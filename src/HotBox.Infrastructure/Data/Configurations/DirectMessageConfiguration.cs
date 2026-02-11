using HotBox.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotBox.Infrastructure.Data.Configurations;

public class DirectMessageConfiguration : IEntityTypeConfiguration<DirectMessage>
{
    public void Configure(EntityTypeBuilder<DirectMessage> builder)
    {
        builder.HasKey(dm => dm.Id);

        builder.Property(dm => dm.Id)
            .ValueGeneratedOnAdd();

        builder.Property(dm => dm.Content)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(dm => dm.SenderId)
            .IsRequired();

        builder.Property(dm => dm.RecipientId)
            .IsRequired();

        builder.Property(dm => dm.CreatedAtUtc)
            .IsRequired();

        builder.HasOne(dm => dm.Sender)
            .WithMany(u => u.SentDirectMessages)
            .HasForeignKey(dm => dm.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(dm => dm.Recipient)
            .WithMany(u => u.ReceivedDirectMessages)
            .HasForeignKey(dm => dm.RecipientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(dm => new { dm.SenderId, dm.CreatedAtUtc });
        builder.HasIndex(dm => new { dm.RecipientId, dm.CreatedAtUtc });
    }
}
