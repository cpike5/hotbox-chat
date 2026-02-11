using HotBox.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotBox.Infrastructure.Data.Configurations;

public class InviteConfiguration : IEntityTypeConfiguration<Invite>
{
    public void Configure(EntityTypeBuilder<Invite> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .ValueGeneratedOnAdd();

        builder.Property(i => i.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(i => i.CreatedByUserId)
            .IsRequired();

        builder.Property(i => i.CreatedAtUtc)
            .IsRequired();

        builder.Property(i => i.UseCount)
            .IsRequired();

        builder.Property(i => i.IsRevoked)
            .IsRequired();

        builder.HasOne(i => i.CreatedBy)
            .WithMany()
            .HasForeignKey(i => i.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.Code).IsUnique();
        builder.HasIndex(i => i.CreatedByUserId);
    }
}
