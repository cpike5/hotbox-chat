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

    public static readonly string[] RoleNames = ["Admin", "Moderator", "Member"];

    public DatabaseSeeder(IServiceProvider serviceProvider, ILogger<DatabaseSeeder> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Database seeding started");

        using var scope = _serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var adminSeedOptions = scope.ServiceProvider.GetRequiredService<IOptions<AdminSeedOptions>>().Value;
        var dbContext = scope.ServiceProvider.GetRequiredService<HotBoxDbContext>();

        await SeedRolesAsync(roleManager);
        var adminUser = await SeedAdminUserAsync(userManager, adminSeedOptions);
        await SeedDefaultChannelsAsync(dbContext, userManager, adminUser);

        _logger.LogInformation("Database seeding completed");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedRolesAsync(RoleManager<IdentityRole<Guid>> roleManager)
    {
        foreach (var roleName in RoleNames)
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                _logger.LogDebug("Role {RoleName} already exists, skipping", roleName);
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
            if (result.Succeeded)
            {
                _logger.LogInformation("Seeded role {RoleName}", roleName);
            }
            else
            {
                _logger.LogError("Failed to seed role {RoleName}: {Errors}", roleName,
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    private async Task<AppUser?> SeedAdminUserAsync(
        UserManager<AppUser> userManager,
        AdminSeedOptions options)
    {
        // Check if AdminSeed config is populated
        if (string.IsNullOrWhiteSpace(options.Email) ||
            string.IsNullOrWhiteSpace(options.Password) ||
            string.IsNullOrWhiteSpace(options.DisplayName))
        {
            _logger.LogWarning("AdminSeed configuration is incomplete, skipping admin user seeding. " +
                               "Set AdminSeed:Email, AdminSeed:Password, and AdminSeed:DisplayName to seed an admin user");
            return null;
        }

        // Check if any user with Admin role already exists
        var admins = await userManager.GetUsersInRoleAsync("Admin");
        if (admins.Count > 0)
        {
            _logger.LogDebug("Admin user already exists, skipping admin seeding");
            return admins[0];
        }

        // Check if a user with the configured email already exists
        var existingUser = await userManager.FindByEmailAsync(options.Email);
        if (existingUser != null)
        {
            _logger.LogInformation("User with email {Email} already exists, assigning Admin role", options.Email);
            var roleResult = await userManager.AddToRoleAsync(existingUser, "Admin");
            if (!roleResult.Succeeded)
            {
                _logger.LogError("Failed to assign Admin role to existing user {Email}: {Errors}",
                    options.Email,
                    string.Join(", ", roleResult.Errors.Select(e => e.Description)));
            }

            return existingUser;
        }

        // Create the admin user
        var adminUser = new AppUser
        {
            UserName = options.Email,
            Email = options.Email,
            DisplayName = options.DisplayName,
            EmailConfirmed = true,
            Status = UserStatus.Offline,
            CreatedAtUtc = DateTime.UtcNow,
            LastSeenUtc = DateTime.UtcNow
        };

        var createResult = await userManager.CreateAsync(adminUser, options.Password);
        if (!createResult.Succeeded)
        {
            _logger.LogError("Failed to create admin user {Email}: {Errors}",
                options.Email,
                string.Join(", ", createResult.Errors.Select(e => e.Description)));
            return null;
        }

        var addRoleResult = await userManager.AddToRoleAsync(adminUser, "Admin");
        if (!addRoleResult.Succeeded)
        {
            _logger.LogError("Failed to assign Admin role to new user {Email}: {Errors}",
                options.Email,
                string.Join(", ", addRoleResult.Errors.Select(e => e.Description)));
            return null;
        }

        _logger.LogInformation("Seeded admin user {Email} with display name {DisplayName}",
            options.Email, options.DisplayName);

        return adminUser;
    }

    private async Task SeedDefaultChannelsAsync(
        HotBoxDbContext dbContext,
        UserManager<AppUser> userManager,
        AppUser? adminUser)
    {
        // If no admin user was provided, try to find one
        if (adminUser == null)
        {
            var admins = await userManager.GetUsersInRoleAsync("Admin");
            if (admins.Count == 0)
            {
                _logger.LogWarning("No admin user exists, skipping default channel seeding. " +
                                   "Channels require a CreatedByUserId");
                return;
            }

            adminUser = admins[0];
        }

        // Only seed channels if none exist
        var hasChannels = await dbContext.Channels.AnyAsync();
        if (hasChannels)
        {
            _logger.LogDebug("Channels already exist, skipping default channel seeding");
            return;
        }

        var now = DateTime.UtcNow;
        var defaultChannels = new List<Channel>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "general",
                Topic = "General discussion",
                Type = ChannelType.Text,
                SortOrder = 0,
                CreatedAtUtc = now,
                CreatedByUserId = adminUser.Id
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "random",
                Topic = "Off-topic conversation",
                Type = ChannelType.Text,
                SortOrder = 1,
                CreatedAtUtc = now,
                CreatedByUserId = adminUser.Id
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "General",
                Type = ChannelType.Voice,
                SortOrder = 0,
                CreatedAtUtc = now,
                CreatedByUserId = adminUser.Id
            }
        };

        dbContext.Channels.AddRange(defaultChannels);
        await dbContext.SaveChangesAsync();

        _logger.LogInformation("Seeded {ChannelCount} default channels (general, random, General voice)",
            defaultChannels.Count);
    }
}
