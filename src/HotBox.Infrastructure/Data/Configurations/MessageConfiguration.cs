using HotBox.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotBox.Infrastructure.Data.Configurations;

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .ValueGeneratedOnAdd();

        builder.Property(m => m.Content)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(m => m.ChannelId)
            .IsRequired();

        builder.Property(m => m.AuthorId)
            .IsRequired();

        builder.Property(m => m.CreatedAtUtc)
            .IsRequired();

        builder.HasOne(m => m.Channel)
            .WithMany(c => c.Messages)
            .HasForeignKey(m => m.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Author)
            .WithMany(u => u.Messages)
            .HasForeignKey(m => m.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new { m.ChannelId, m.CreatedAtUtc });
        builder.HasIndex(m => m.AuthorId);
    }
}
