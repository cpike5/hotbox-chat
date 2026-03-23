using HotBox.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotBox.Infrastructure.Data.Configurations;

public class UserChannelReadConfiguration : IEntityTypeConfiguration<UserChannelRead>
{
    public void Configure(EntityTypeBuilder<UserChannelRead> builder)
    {
        builder.HasKey(ucr => new { ucr.UserId, ucr.ChannelId });

        builder.HasOne(ucr => ucr.User)
            .WithMany()
            .HasForeignKey(ucr => ucr.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ucr => ucr.Channel)
            .WithMany()
            .HasForeignKey(ucr => ucr.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ucr => ucr.LastReadMessage)
            .WithMany()
            .HasForeignKey(ucr => ucr.LastReadMessageId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
