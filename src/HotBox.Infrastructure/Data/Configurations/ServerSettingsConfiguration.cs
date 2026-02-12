using HotBox.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotBox.Infrastructure.Data.Configurations;

public class ServerSettingsConfiguration : IEntityTypeConfiguration<ServerSettings>
{
    public void Configure(EntityTypeBuilder<ServerSettings> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .ValueGeneratedOnAdd();

        builder.Property(s => s.ServerName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.RegistrationMode)
            .IsRequired();
    }
}
