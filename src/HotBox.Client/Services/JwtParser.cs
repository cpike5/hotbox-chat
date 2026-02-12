using System.Security.Claims;
using System.Text.Json;
using HotBox.Client.Models;
using HotBox.Core.Enums;

namespace HotBox.Client.Services;

/// <summary>
/// Parses JWT access tokens on the client side to extract user claims
/// without requiring the System.IdentityModel.Tokens.Jwt package.
/// </summary>
public static class JwtParser
{
    /// <summary>
    /// Builds a <see cref="UserInfo"/> from the claims in a JWT access token.
    /// Falls back to sensible defaults if the token cannot be parsed.
    /// </summary>
    public static UserInfo ParseUserInfoFromToken(string accessToken)
    {
        var claims = ParseClaimsFromJwt(accessToken);

        var userInfo = new UserInfo
        {
            DisplayName = "User",
            Role = "Member",
        };

        foreach (var claim in claims)
        {
            switch (claim.Type)
            {
                case "sub":
                case ClaimTypes.NameIdentifier:
                    if (Guid.TryParse(claim.Value, out var id))
                        userInfo.Id = id;
                    break;

                case "display_name":
                    userInfo.DisplayName = claim.Value;
                    break;

                case "role":
                case ClaimTypes.Role:
                    userInfo.Role = claim.Value;
                    break;
            }
        }

        return userInfo;
    }

    private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length != 3)
            return [];

        var payload = parts[1];
        var jsonBytes = ParseBase64WithoutPadding(payload);

        using var document = JsonDocument.Parse(jsonBytes);
        var claims = new List<Claim>();

        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in property.Value.EnumerateArray())
                {
                    claims.Add(new Claim(property.Name, item.GetString() ?? string.Empty));
                }
            }
            else
            {
                claims.Add(new Claim(property.Name, property.Value.ToString()));
            }
        }

        return claims;
    }

    private static byte[] ParseBase64WithoutPadding(string base64)
    {
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }

        return Convert.FromBase64String(base64.Replace('-', '+').Replace('_', '/'));
    }
}
