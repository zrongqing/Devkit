namespace Devkit.Business.Infrastructure.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "Devkit.Server";
    public string Audience { get; set; } = "Devkit.Clients";
    public string ValidationPublicKeyPath { get; set; } = string.Empty;
}
