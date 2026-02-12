using FluentAssertions;
using HotBox.Core.Entities;
using HotBox.Core.Enums;
using HotBox.Core.Options;
using HotBox.Infrastructure.Data;
using HotBox.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace HotBox.Infrastructure.Tests.Services;

public class ServerSettingsServiceTests
{
    private readonly HotBoxDbContext _context;
    private readonly IOptions<ServerOptions> _serverOptions;
    private readonly ILogger<ServerSettingsService> _logger;
    private readonly ServerSettingsService _sut;

    public ServerSettingsServiceTests()
    {
        var options = new DbContextOptionsBuilder<HotBoxDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _context = new HotBoxDbContext(options);
        _serverOptions = Options.Create(new ServerOptions
        {
            ServerName = "Default Server",
            RegistrationMode = RegistrationMode.Open,
            Port = 5000
        });
        _logger = Substitute.For<ILogger<ServerSettingsService>>();
        _sut = new ServerSettingsService(_context, _serverOptions, _logger);
    }

    [Fact]
    public async Task GetAsync_WithNoSettings_ReturnsFallbackFromOptions()
    {
        // Act
        var result = await _sut.GetAsync();

        // Assert
        result.Should().NotBeNull();
        result.ServerName.Should().Be("Default Server");
        result.RegistrationMode.Should().Be(RegistrationMode.Open);
        result.Id.Should().Be(Guid.Empty); // Fallback doesn't have real ID
    }

    [Fact]
    public async Task GetAsync_WithExistingSettings_ReturnsSettings()
    {
        // Arrange
        var settings = new ServerSettings
        {
            Id = Guid.NewGuid(),
            ServerName = "My Server",
            RegistrationMode = RegistrationMode.InviteOnly
        };
        _context.ServerSettings.Add(settings);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetAsync();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(settings.Id);
        result.ServerName.Should().Be("My Server");
        result.RegistrationMode.Should().Be(RegistrationMode.InviteOnly);
    }

    [Fact]
    public async Task UpdateAsync_WithNoExistingSettings_CreatesSettings()
    {
        // Act
        var result = await _sut.UpdateAsync("New Server", RegistrationMode.Closed);

        // Assert
        result.Should().NotBeNull();
        result.ServerName.Should().Be("New Server");
        result.RegistrationMode.Should().Be(RegistrationMode.Closed);
        result.Id.Should().NotBe(Guid.Empty);

        // Verify it was saved
        var saved = await _context.ServerSettings.FirstOrDefaultAsync();
        saved.Should().NotBeNull();
        saved!.ServerName.Should().Be("New Server");
    }

    [Fact]
    public async Task UpdateAsync_WithExistingSettings_UpdatesSettings()
    {
        // Arrange
        var existingId = Guid.NewGuid();
        var settings = new ServerSettings
        {
            Id = existingId,
            ServerName = "Old Name",
            RegistrationMode = RegistrationMode.Open
        };
        _context.ServerSettings.Add(settings);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.UpdateAsync("Updated Name", RegistrationMode.InviteOnly);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(existingId); // Should keep same ID
        result.ServerName.Should().Be("Updated Name");
        result.RegistrationMode.Should().Be(RegistrationMode.InviteOnly);

        // Verify only one record exists
        var count = await _context.ServerSettings.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        // Arrange & Act
        await _sut.UpdateAsync("Persisted Server", RegistrationMode.Closed);

        // Assert - create new service instance to verify persistence
        var newService = new ServerSettingsService(_context, _serverOptions, _logger);
        var result = await newService.GetAsync();

        result.ServerName.Should().Be("Persisted Server");
        result.RegistrationMode.Should().Be(RegistrationMode.Closed);
    }

    [Fact]
    public async Task UpdateAsync_AllRegistrationModes_Work()
    {
        // Test Open mode
        await _sut.UpdateAsync("Server", RegistrationMode.Open);
        _context.ChangeTracker.Clear();
        var result1 = await _context.ServerSettings.FirstOrDefaultAsync();
        result1!.RegistrationMode.Should().Be(RegistrationMode.Open);

        // Test InviteOnly mode
        await _sut.UpdateAsync("Server", RegistrationMode.InviteOnly);
        _context.ChangeTracker.Clear();
        var result2 = await _context.ServerSettings.FirstOrDefaultAsync();
        result2!.RegistrationMode.Should().Be(RegistrationMode.InviteOnly);

        // Test Closed mode
        await _sut.UpdateAsync("Server", RegistrationMode.Closed);
        _context.ChangeTracker.Clear();
        var result3 = await _context.ServerSettings.FirstOrDefaultAsync();
        result3!.RegistrationMode.Should().Be(RegistrationMode.Closed);
    }
}
