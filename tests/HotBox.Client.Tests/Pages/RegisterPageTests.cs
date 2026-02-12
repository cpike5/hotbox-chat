using Bunit;
using FluentAssertions;
using HotBox.Client.Pages;
using HotBox.Client.Services;
using HotBox.Client.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text.Json;

namespace HotBox.Client.Tests.Pages;

/// <summary>
/// Tests for RegisterPage component covering registration form, validation, and registration modes.
/// </summary>
public class RegisterPageTests : TestContext
{
    private readonly AuthState _authState;
    private readonly ApiClient _apiClient;

    public RegisterPageTests()
    {
        _authState = new AuthState();

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
    public void Render_ShowsRegistrationForm()
    {
        // Act
        var cut = RenderComponent<RegisterPage>();

        // Assert
        var form = cut.Find("form");
        form.Should().NotBeNull();
    }

    [Fact]
    public void Render_ShowsRequiredFormFields()
    {
        // Act
        var cut = RenderComponent<RegisterPage>();

        // Assert
        var usernameInput = cut.Find("input[type='text'][autocomplete='username']");
        usernameInput.Should().NotBeNull();

        var emailInput = cut.Find("input[type='email']");
        emailInput.Should().NotBeNull();
        emailInput.GetAttribute("autocomplete").Should().Be("email");

        var passwordInput = cut.Find("input[type='password'][autocomplete='new-password']");
        passwordInput.Should().NotBeNull();
    }

    [Fact]
    public void Render_ShowsConfirmPasswordField()
    {
        // Act
        var cut = RenderComponent<RegisterPage>();

        // Assert
        var confirmPasswordInputs = cut.FindAll("input[autocomplete='new-password']");
        confirmPasswordInputs.Should().HaveCount(2, "password and confirm password fields should exist");
    }

    [Fact]
    public void Render_ShowsSubmitButton()
    {
        // Act
        var cut = RenderComponent<RegisterPage>();

        // Assert
        var submitButton = cut.Find("button[type='submit']");
        submitButton.Should().NotBeNull();
        submitButton.TextContent.Should().Contain("Create Account");
    }

    [Fact]
    public void Render_ShowsLoginLink()
    {
        // Act
        var cut = RenderComponent<RegisterPage>();

        // Assert
        var loginLink = cut.Find("a[href='/login']");
        loginLink.Should().NotBeNull();
        loginLink.TextContent.Should().Contain("Sign in");
    }

    [Fact]
    public void Render_ShowsPasswordToggleButtons()
    {
        // Act
        var cut = RenderComponent<RegisterPage>();

        // Assert
        var toggleButtons = cut.FindAll("button.toggle-password");
        toggleButtons.Should().HaveCount(2, "password and confirm password should each have a toggle button");
    }

    [Fact]
    public void Render_WhenOpenMode_ShowsOpenBanner()
    {
        // Act
        var cut = RenderComponent<RegisterPage>();

        // Assert - wait for the component to load registration mode
        cut.WaitForState(() => cut.FindAll(".reg-banner.open").Count > 0, timeout: TimeSpan.FromSeconds(2));

        var banner = cut.Find(".reg-banner.open");
        banner.TextContent.Should().Contain("Registration is currently open");
    }

    [Fact]
    public void Render_WhenOpenMode_DoesNotShowInviteCodeField()
    {
        // Act
        var cut = RenderComponent<RegisterPage>();

        // Assert - wait for component to load
        cut.WaitForState(() => cut.FindAll(".reg-banner").Count > 0, timeout: TimeSpan.FromSeconds(2));

        var inviteCodeInputs = cut.FindAll("input[placeholder*='invite code']");
        inviteCodeInputs.Should().BeEmpty("invite code field should not be visible in Open mode");
    }

    [Fact]
    public void Render_ShowsWelcomeMessage()
    {
        // Act
        var cut = RenderComponent<RegisterPage>();

        // Assert
        var heading = cut.Find("h1");
        heading.TextContent.Should().Be("Create an account");

        var brand = cut.Find(".auth-card-brand");
        brand.TextContent.Should().Be("HotBox");
    }

    [Fact]
    public void Render_ShowsBrandName()
    {
        // Act
        var cut = RenderComponent<RegisterPage>();

        // Assert - PageTitle is metadata and not rendered as a DOM element in bUnit;
        // verify the brand name is visible in the auth card instead
        var brand = cut.Find(".auth-card-brand");
        brand.TextContent.Should().Contain("HotBox");
    }

    [Fact]
    public void PasswordToggle_ChangesInputType()
    {
        // Arrange
        var cut = RenderComponent<RegisterPage>();
        var passwordInputs = cut.FindAll("input[autocomplete='new-password']");
        var toggleButtons = cut.FindAll("button.toggle-password");

        // Initial state - passwords hidden
        passwordInputs[0].GetAttribute("type").Should().Be("password");
        passwordInputs[1].GetAttribute("type").Should().Be("password");

        // Act - toggle first password visibility
        toggleButtons[0].Click();

        // Assert - first password visible
        passwordInputs = cut.FindAll("input[autocomplete='new-password']");
        passwordInputs[0].GetAttribute("type").Should().Be("text");
        passwordInputs[1].GetAttribute("type").Should().Be("password");
    }

    [Fact]
    public void Render_ShowsFormLabels()
    {
        // Act
        var cut = RenderComponent<RegisterPage>();

        // Assert
        var labels = cut.FindAll(".form-label");
        var labelTexts = labels.Select(l => l.TextContent).ToList();

        labelTexts.Should().Contain("Username");
        labelTexts.Should().Contain("Email");
        labelTexts.Should().Contain("Password");
        labelTexts.Should().Contain("Confirm Password");
    }

    // Mock HttpMessageHandler that returns registration mode
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // Return Open registration mode
            if (request.RequestUri?.AbsolutePath.Contains("registration-mode") == true)
            {
                var response = new { Mode = "Open" };
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(response))
                });
            }

            // Return empty provider list
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
