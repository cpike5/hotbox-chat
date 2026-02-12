using Bunit;
using FluentAssertions;
using HotBox.Client.Pages;
using HotBox.Client.Services;
using HotBox.Client.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Net;

namespace HotBox.Client.Tests.Pages;

/// <summary>
/// Tests for LoginPage component covering form rendering, input fields, and OAuth providers.
/// </summary>
public class LoginPageTests : TestContext
{
    private readonly AuthState _authState;
    private readonly ApiClient _apiClient;

    public LoginPageTests()
    {
        _authState = new AuthState();

        // Create a mock HttpClient and ApiClient
        var httpClient = new HttpClient(new MockHttpMessageHandler())
        {
            BaseAddress = new Uri("http://localhost")
        };
        var logger = new NullLogger<ApiClient>();
        _apiClient = new ApiClient(httpClient, _authState, logger);

        Services.AddSingleton(_authState);
        Services.AddSingleton(_apiClient);
    }

    [Fact]
    public void Render_ShowsLoginForm()
    {
        // Act
        var cut = RenderComponent<LoginPage>();

        // Assert
        var form = cut.Find("form");
        form.Should().NotBeNull();
    }

    [Fact]
    public void Render_ShowsEmailAndPasswordFields()
    {
        // Act
        var cut = RenderComponent<LoginPage>();

        // Assert
        var emailInput = cut.Find("input#loginEmail");
        emailInput.Should().NotBeNull();
        emailInput.GetAttribute("type").Should().Be("email");
        emailInput.GetAttribute("autocomplete").Should().Be("email");

        var passwordInput = cut.Find("input#loginPassword");
        passwordInput.Should().NotBeNull();
        passwordInput.GetAttribute("type").Should().Be("password");
        passwordInput.GetAttribute("autocomplete").Should().Be("current-password");
    }

    [Fact]
    public void Render_ShowsSubmitButton()
    {
        // Act
        var cut = RenderComponent<LoginPage>();

        // Assert
        var submitButton = cut.Find("button[type='submit']");
        submitButton.Should().NotBeNull();
        submitButton.TextContent.Should().Contain("Sign In");
    }

    [Fact]
    public void Render_ShowsRememberMeCheckbox()
    {
        // Act
        var cut = RenderComponent<LoginPage>();

        // Assert
        var checkbox = cut.Find("input[type='checkbox']");
        checkbox.Should().NotBeNull();

        var label = cut.Find(".form-check");
        label.TextContent.Should().Contain("Remember me");
    }

    [Fact]
    public void Render_ShowsForgotPasswordLink()
    {
        // Act
        var cut = RenderComponent<LoginPage>();

        // Assert
        var forgotPasswordLink = cut.Find("a[href='/forgot-password']");
        forgotPasswordLink.Should().NotBeNull();
        forgotPasswordLink.TextContent.Should().Contain("Forgot password");
    }

    [Fact]
    public void Render_ShowsRegisterLink()
    {
        // Act
        var cut = RenderComponent<LoginPage>();

        // Assert
        var registerLink = cut.Find("a[href='/register']");
        registerLink.Should().NotBeNull();
        registerLink.TextContent.Should().Contain("Create one");
    }

    [Fact]
    public void Render_ShowsPasswordToggleButton()
    {
        // Act
        var cut = RenderComponent<LoginPage>();

        // Assert
        var toggleButton = cut.Find("button.toggle-password");
        toggleButton.Should().NotBeNull();

        var svg = toggleButton.QuerySelector("svg");
        svg.Should().NotBeNull("toggle button should have an icon");
    }

    [Fact]
    public void PasswordToggle_ChangesInputType()
    {
        // Arrange
        var cut = RenderComponent<LoginPage>();
        var passwordInput = cut.Find("input#loginPassword");
        var toggleButton = cut.Find("button.toggle-password");

        // Initial state - password hidden
        passwordInput.GetAttribute("type").Should().Be("password");

        // Act - toggle password visibility
        toggleButton.Click();

        // Assert - password visible
        passwordInput = cut.Find("input#loginPassword");
        passwordInput.GetAttribute("type").Should().Be("text");

        // Act - toggle again
        toggleButton.Click();

        // Assert - password hidden again
        passwordInput = cut.Find("input#loginPassword");
        passwordInput.GetAttribute("type").Should().Be("password");
    }

    [Fact]
    public void Render_ShowsWelcomeMessage()
    {
        // Act
        var cut = RenderComponent<LoginPage>();

        // Assert
        var heading = cut.Find("h1");
        heading.TextContent.Should().Be("Welcome back");

        var brand = cut.Find(".auth-card-brand");
        brand.TextContent.Should().Be("HotBox");
    }

    [Fact]
    public void Render_ShowsBrandName()
    {
        // Act
        var cut = RenderComponent<LoginPage>();

        // Assert - PageTitle is metadata and not rendered as a DOM element in bUnit;
        // verify the brand name is visible in the auth card instead
        var brand = cut.Find(".auth-card-brand");
        brand.TextContent.Should().Contain("HotBox");
    }

    // Mock HttpMessageHandler for ApiClient
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // Return empty provider list for the GetProvidersAsync call
            if (request.RequestUri?.AbsolutePath.Contains("providers") == true)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
