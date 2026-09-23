namespace Devkit.Server.Infrastructure.Configuration;

public sealed class BootstrapAccountOptions
{
    public const string SectionName = "BootstrapAccount";

    public bool Enabled { get; set; } = true;
    public string UserName { get; set; } = "admin";
    public string Email { get; set; } = "admin@example.invalid";
    public string Password { get; set; } = "admin";
    public bool IsBuiltInDefault => UserName == "admin" && Password == "admin";
}
