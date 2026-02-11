using HotBox.Core.Entities;
using HotBox.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotBox.Infrastructure.Data.Configurations;

public class ChannelConfiguration : IEntityTypeConfiguration<Channel>
{
    public void Configure(EntityTypeBuilder<Channel> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .ValueGeneratedOnAdd();

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.Topic)
            .HasMaxLength(500);

        builder.Property(c => c.Type)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(c => c.SortOrder)
            .IsRequired();

        builder.Property(c => c.CreatedAtUtc)
            .IsRequired();

        builder.Property(c => c.CreatedByUserId)
            .IsRequired();

        builder.HasOne(c => c.CreatedBy)
            .WithMany()
            .HasForeignKey(c => c.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Messages)
            .WithOne(m => m.Channel)
            .HasForeignKey(m => m.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.CreatedByUserId);
    }
}
