using Microsoft.AspNetCore.Authentication;

namespace HotBox.Application.Authentication;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "ApiKey";
}
