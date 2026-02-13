using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using HotBox.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HotBox.Application.Authentication;

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    private readonly HotBoxDbContext _dbContext;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        HotBoxDbContext dbContext)
        : base(options, logger, encoder)
    {
        _dbContext = dbContext;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var headerValue))
        {
            return AuthenticateResult.NoResult();
        }

        var plaintextKey = headerValue.ToString();
        if (string.IsNullOrWhiteSpace(plaintextKey))
        {
            return AuthenticateResult.NoResult();
        }

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(plaintextKey));
        var keyHash = Convert.ToBase64String(hashBytes);

        var apiKey = await _dbContext.ApiKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(ak => ak.KeyValue == keyHash, Context.RequestAborted);

        if (apiKey is null)
        {
            Logger.LogWarning("API key authentication failed: key not found");
            return AuthenticateResult.Fail("Invalid API key.");
        }

        if (apiKey.RevokedAtUtc.HasValue)
        {
            Logger.LogWarning("API key authentication failed: key {ApiKeyId} is revoked", apiKey.Id);
            return AuthenticateResult.Fail("API key has been revoked.");
        }

        var claims = new[]
        {
            new Claim("auth_method", "api_key"),
            new Claim("api_key_id", apiKey.Id.ToString()),
            new Claim("api_key_name", apiKey.Name),
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        Logger.LogInformation("API key {ApiKeyId} ({ApiKeyName}) authenticated successfully", apiKey.Id, apiKey.Name);

        return AuthenticateResult.Success(ticket);
    }
}
