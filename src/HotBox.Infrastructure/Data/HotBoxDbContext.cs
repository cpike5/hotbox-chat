using System.Reflection;
using HotBox.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HotBox.Infrastructure.Data;

public class HotBoxDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
{
    public HotBoxDbContext(DbContextOptions<HotBoxDbContext> options) : base(options)
    {
    }

    public DbSet<Channel> Channels => Set<Channel>();

    public DbSet<Message> Messages => Set<Message>();

    public DbSet<DirectMessage> DirectMessages => Set<DirectMessage>();

    public DbSet<Invite> Invites => Set<Invite>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
