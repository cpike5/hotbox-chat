using HotBox.Core.Entities;
using HotBox.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotBox.Infrastructure.Data.Configurations;

public class ServerSettingsConfiguration : IEntityTypeConfiguration<ServerSettings>
{
    public static readonly Guid DefaultSettingsId = new("00000000-0000-0000-0000-000000000001");

    public void Configure(EntityTypeBuilder<ServerSettings> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.ServerName)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasData(new ServerSettings
        {
            Id = DefaultSettingsId,
            ServerName = "HotBox",
            RegistrationMode = RegistrationMode.InviteOnly
        });
    }
}
