using HotBox.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotBox.Infrastructure.Data.Configurations;

public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.HasKey(k => k.Id);

        builder.Property(k => k.KeyHash)
            .IsRequired();

        builder.Property(k => k.KeyPrefix)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(k => k.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(k => k.KeyPrefix);

        builder.Ignore(k => k.IsRevoked);
        builder.Ignore(k => k.IsActive);
    }
}
