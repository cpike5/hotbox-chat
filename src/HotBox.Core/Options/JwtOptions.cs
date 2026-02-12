namespace HotBox.Core.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Secret { get; set; } = string.Empty;

    public string Issuer { get; set; } = "HotBox";

    public string Audience { get; set; } = "HotBox";

    public TimeSpan AccessTokenExpiration { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan RefreshTokenExpiration { get; set; } = TimeSpan.FromDays(7);
}
