using System.Net;
using FluentAssertions;
using HotBox.Application.Controllers;
using HotBox.Application.Models;
using HotBox.Core.Entities;
using HotBox.Core.Interfaces;
using HotBox.Core.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace HotBox.Application.Tests.Controllers;

public class DemoControllerTests
{
    private readonly IDemoUserService _demoUserService;
    private readonly ITokenService _tokenService;
    private readonly ILogger<DemoController> _logger;

    public DemoControllerTests()
    {
        _demoUserService = Substitute.For<IDemoUserService>();
        _tokenService = Substitute.For<ITokenService>();
        _logger = Substitute.For<ILogger<DemoController>>();
    }

    private DemoController CreateSut(DemoModeOptions options, string remoteIp = "1.2.3.4")
    {
        var optionsWrapper = Options.Create(options);
        var sut = new DemoController(_demoUserService, _tokenService, optionsWrapper, _logger);
        sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                Connection = { RemoteIpAddress = IPAddress.Parse(remoteIp) }
            }
        };
        return sut;
    }

    // -------------------------------------------------------------------------
    // Register
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Register_WhenDisabled_Returns404()
    {
        // Arrange
        var options = new DemoModeOptions { Enabled = false };
        var sut = CreateSut(options);
        var request = new DemoRegisterRequest { Username = "alice", DisplayName = "Alice" };

        // Act
        var result = await sut.Register(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Register_WhenIpCoolingDown_Returns429()
    {
        // Arrange
        var options = new DemoModeOptions { Enabled = true, MaxConcurrentUsers = 50 };
        var sut = CreateSut(options, remoteIp: "10.0.0.1");
        var request = new DemoRegisterRequest { Username = "alice", DisplayName = "Alice" };

        _demoUserService.IsIpCoolingDownAsync("10.0.0.1", Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await sut.Register(request, CancellationToken.None);

        // Assert
        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(429);
    }

    [Fact]
    public async Task Register_WhenCapacityFull_Returns409()
    {
        // Arrange
        var options = new DemoModeOptions { Enabled = true, MaxConcurrentUsers = 50 };
        var sut = CreateSut(options);
        var request = new DemoRegisterRequest { Username = "alice", DisplayName = "Alice" };

        _demoUserService.IsIpCoolingDownAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _demoUserService.GetActiveDemoUserCountAsync(Arg.Any<CancellationToken>())
            .Returns(50);

        // Act
        var result = await sut.Register(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task Register_WhenCapacityAtMax_Returns409()
    {
        // Arrange — exactly at the limit (>= max) should still reject
        var options = new DemoModeOptions { Enabled = true, MaxConcurrentUsers = 10 };
        var sut = CreateSut(options);
        var request = new DemoRegisterRequest { Username = "bob", DisplayName = "Bob" };

        _demoUserService.IsIpCoolingDownAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _demoUserService.GetActiveDemoUserCountAsync(Arg.Any<CancellationToken>())
            .Returns(10);

        // Act
        var result = await sut.Register(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task Register_WhenCreateDemoUserReturnsNull_ReturnsBadRequest()
    {
        // Arrange
        var options = new DemoModeOptions { Enabled = true, MaxConcurrentUsers = 50 };
        var sut = CreateSut(options);
        var request = new DemoRegisterRequest { Username = "alice", DisplayName = "Alice" };

        _demoUserService.IsIpCoolingDownAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _demoUserService.GetActiveDemoUserCountAsync(Arg.Any<CancellationToken>())
            .Returns(0);
        _demoUserService.CreateDemoUserAsync(
                request.Username, request.DisplayName, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((AppUser?)null);

        // Act
        var result = await sut.Register(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Register_Success_ReturnsAuthResponse()
    {
        // Arrange
        var options = new DemoModeOptions { Enabled = true, MaxConcurrentUsers = 50 };
        var sut = CreateSut(options, remoteIp: "192.168.1.1");
        var request = new DemoRegisterRequest { Username = "alice", DisplayName = "Alice Demo" };

        var createdUser = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = "demo_alice_x7k2",
            Email = "demo_alice_x7k2@demo.local",
            DisplayName = "Alice Demo",
            IsDemo = true
        };
        var expectedToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.test";

        _demoUserService.IsIpCoolingDownAsync("192.168.1.1", Arg.Any<CancellationToken>())
            .Returns(false);
        _demoUserService.GetActiveDemoUserCountAsync(Arg.Any<CancellationToken>())
            .Returns(5);
        _demoUserService.CreateDemoUserAsync(
                request.Username, request.DisplayName, "192.168.1.1", Arg.Any<CancellationToken>())
            .Returns(createdUser);
        _tokenService.GenerateAccessTokenAsync(createdUser, Arg.Any<CancellationToken>())
            .Returns(expectedToken);

        // Act
        var result = await sut.Register(request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var authResponse = okResult.Value.Should().BeOfType<AuthResponse>().Subject;
        authResponse.AccessToken.Should().Be(expectedToken);

        await _tokenService.Received(1).GenerateAccessTokenAsync(createdUser, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_Success_CallsCreateDemoUserWithCorrectIpAddress()
    {
        // Arrange
        var options = new DemoModeOptions { Enabled = true, MaxConcurrentUsers = 50 };
        var remoteIp = "203.0.113.5";
        var sut = CreateSut(options, remoteIp);
        var request = new DemoRegisterRequest { Username = "charlie", DisplayName = "Charlie" };

        var createdUser = new AppUser { Id = Guid.NewGuid(), UserName = "demo_charlie_ab12", IsDemo = true };

        _demoUserService.IsIpCoolingDownAsync(remoteIp, Arg.Any<CancellationToken>())
            .Returns(false);
        _demoUserService.GetActiveDemoUserCountAsync(Arg.Any<CancellationToken>())
            .Returns(0);
        _demoUserService.CreateDemoUserAsync(
                request.Username, request.DisplayName, remoteIp, Arg.Any<CancellationToken>())
            .Returns(createdUser);
        _tokenService.GenerateAccessTokenAsync(Arg.Any<AppUser>(), Arg.Any<CancellationToken>())
            .Returns("token");

        // Act
        await sut.Register(request, CancellationToken.None);

        // Assert
        await _demoUserService.Received(1).CreateDemoUserAsync(
            request.Username, request.DisplayName, remoteIp, Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Status
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Status_WhenDisabled_ReturnsDisabledWithZeroCounts()
    {
        // Arrange
        var options = new DemoModeOptions { Enabled = false, MaxConcurrentUsers = 50 };
        var sut = CreateSut(options);

        // Act
        var result = await sut.Status(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value!;
        var type = value.GetType();

        type.GetProperty("enabled")!.GetValue(value).Should().Be(false);
        type.GetProperty("currentUsers")!.GetValue(value).Should().Be(0);
        type.GetProperty("maxUsers")!.GetValue(value).Should().Be(50);
    }

    [Fact]
    public async Task Status_WhenDisabled_DoesNotQueryActiveUserCount()
    {
        // Arrange
        var options = new DemoModeOptions { Enabled = false, MaxConcurrentUsers = 50 };
        var sut = CreateSut(options);

        // Act
        await sut.Status(CancellationToken.None);

        // Assert — service should not be called when demo mode is off
        await _demoUserService.DidNotReceive().GetActiveDemoUserCountAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Status_WhenEnabled_ReturnsCorrectCounts()
    {
        // Arrange
        var options = new DemoModeOptions { Enabled = true, MaxConcurrentUsers = 50 };
        var sut = CreateSut(options);

        _demoUserService.GetActiveDemoUserCountAsync(Arg.Any<CancellationToken>())
            .Returns(23);

        // Act
        var result = await sut.Status(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value!;
        var type = value.GetType();

        type.GetProperty("enabled")!.GetValue(value).Should().Be(true);
        type.GetProperty("currentUsers")!.GetValue(value).Should().Be(23);
        type.GetProperty("maxUsers")!.GetValue(value).Should().Be(50);
    }

    [Fact]
    public async Task Status_WhenEnabled_ReflectsConfiguredMaxUsers()
    {
        // Arrange
        var options = new DemoModeOptions { Enabled = true, MaxConcurrentUsers = 25 };
        var sut = CreateSut(options);

        _demoUserService.GetActiveDemoUserCountAsync(Arg.Any<CancellationToken>())
            .Returns(0);

        // Act
        var result = await sut.Status(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var type = okResult.Value!.GetType();
        type.GetProperty("maxUsers")!.GetValue(okResult.Value).Should().Be(25);
    }
}
