using HotBox.Core.Entities;
using HotBox.Core.Enums;
using HotBox.Core.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HotBox.Infrastructure.Data.Seeding;

public class DatabaseSeeder : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(IServiceProvider serviceProvider, ILogger<DatabaseSeeder> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HotBoxDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var adminOptions = scope.ServiceProvider.GetRequiredService<IOptions<AdminSeedOptions>>().Value;

        await dbContext.Database.MigrateAsync(ct);

        await SeedRolesAsync(roleManager);
        await SeedAdminUserAsync(userManager, adminOptions);
        await SeedDefaultChannelAsync(dbContext, userManager, adminOptions, ct);

        _logger.LogInformation("Database seeding completed");
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    private async Task SeedRolesAsync(RoleManager<IdentityRole<Guid>> roleManager)
    {
        var roles = new[] { nameof(UserRole.Admin), nameof(UserRole.Moderator), nameof(UserRole.Member) };

        foreach (var roleName in roles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid> { Name = roleName });
                _logger.LogInformation("Created role {RoleName}", roleName);
            }
        }
    }

    private async Task SeedAdminUserAsync(UserManager<AppUser> userManager, AdminSeedOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Email))
        {
            _logger.LogWarning("AdminSeed:Email not configured, skipping admin user creation");
            return;
        }

        var existingAdmin = await userManager.FindByEmailAsync(options.Email);
        if (existingAdmin is not null)
        {
            return;
        }

        var admin = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = options.Email,
            Email = options.Email,
            DisplayName = string.IsNullOrWhiteSpace(options.DisplayName) ? "Admin" : options.DisplayName,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
            Status = UserStatus.Offline
        };

        var result = await userManager.CreateAsync(admin, options.Password);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, nameof(UserRole.Admin));
            _logger.LogInformation("Created admin user {Email}", options.Email);
        }
        else
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogError("Failed to create admin user: {Errors}", errors);
        }
    }

    private async Task SeedDefaultChannelAsync(
        HotBoxDbContext dbContext,
        UserManager<AppUser> userManager,
        AdminSeedOptions options,
        CancellationToken ct)
    {
        if (await dbContext.Channels.AnyAsync(ct))
        {
            return;
        }

        var admin = await userManager.FindByEmailAsync(options.Email);
        if (admin is null)
        {
            _logger.LogWarning("Admin user not found, skipping default channel creation");
            return;
        }

        var generalChannel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "general",
            Description = "General discussion",
            Type = ChannelType.Text,
            SortOrder = 0,
            CreatedAt = DateTime.UtcNow,
            CreatedById = admin.Id
        };

        dbContext.Channels.Add(generalChannel);
        await dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Created default channel {ChannelName}", generalChannel.Name);
    }
}
