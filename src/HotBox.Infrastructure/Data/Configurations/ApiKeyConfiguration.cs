using HotBox.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotBox.Infrastructure.Data.Configurations;

public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.HasKey(ak => ak.Id);

        builder.Property(ak => ak.Id)
            .ValueGeneratedOnAdd();

        builder.Property(ak => ak.KeyValue)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(ak => ak.KeyPrefix)
            .IsRequired()
            .HasMaxLength(8);

        builder.Property(ak => ak.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(ak => ak.CreatedAtUtc)
            .IsRequired();

        builder.Property(ak => ak.RevokedReason)
            .HasMaxLength(500);

        builder.Ignore(ak => ak.IsRevoked);
        builder.Ignore(ak => ak.IsActive);

        builder.HasMany(ak => ak.CreatedAgents)
            .WithOne(u => u.CreatedByApiKey)
            .HasForeignKey(u => u.CreatedByApiKeyId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(ak => ak.KeyValue).IsUnique();
    }
}
