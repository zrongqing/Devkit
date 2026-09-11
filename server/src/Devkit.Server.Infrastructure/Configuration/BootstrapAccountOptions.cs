namespace Devkit.Server.Infrastructure.Configuration;

public sealed class BootstrapAccountOptions
{
    public const string SectionName = "BootstrapAccount";

    public bool Enabled { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
