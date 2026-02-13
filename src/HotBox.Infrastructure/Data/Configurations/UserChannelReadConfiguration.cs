using HotBox.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotBox.Infrastructure.Data.Configurations;

public class UserChannelReadConfiguration : IEntityTypeConfiguration<UserChannelRead>
{
    public void Configure(EntityTypeBuilder<UserChannelRead> builder)
    {
        builder.HasKey(ucr => new { ucr.UserId, ucr.ChannelId });

        builder.Property(ucr => ucr.UserId)
            .IsRequired();

        builder.Property(ucr => ucr.ChannelId)
            .IsRequired();

        builder.Property(ucr => ucr.LastReadAtUtc)
            .IsRequired();

        builder.HasOne(ucr => ucr.User)
            .WithMany()
            .HasForeignKey(ucr => ucr.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ucr => ucr.Channel)
            .WithMany()
            .HasForeignKey(ucr => ucr.ChannelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ucr => ucr.LastReadMessage)
            .WithMany()
            .HasForeignKey(ucr => ucr.LastReadMessageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
