using HotBox.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;

namespace HotBox.Infrastructure.Data.Configurations;

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Content)
            .IsRequired()
            .HasMaxLength(4000);

        builder.HasIndex(m => new { m.ChannelId, m.CreatedAt });

        builder.HasIndex(m => m.UserId);

        builder.HasOne(m => m.User)
            .WithMany(u => u.Messages)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // PostgreSQL full-text search via shadow property
        builder.Property<NpgsqlTsVector>("SearchVector")
            .HasColumnName("search_vector")
            .IsGeneratedTsVectorColumn("english", "Content");

        builder.HasIndex("SearchVector")
            .HasMethod("GIN");
    }
}
