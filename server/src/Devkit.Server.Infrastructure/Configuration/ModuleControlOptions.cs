namespace Devkit.Server.Infrastructure.Configuration;

public sealed class ModuleControlOptions
{
    public const string SectionName = "ModuleControl";

    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public int OfflineAfterSeconds { get; set; } = 30;
    public int DefaultLeaseSeconds { get; set; } = 60;
    public int MaximumLeaseSeconds { get; set; } = 300;
    public int MaximumPayloadBytes { get; set; } = 262144;
}
