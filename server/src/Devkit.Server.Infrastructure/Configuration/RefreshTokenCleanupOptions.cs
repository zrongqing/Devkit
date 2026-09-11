namespace Devkit.Server.Infrastructure.Configuration;

public sealed class RefreshTokenCleanupOptions
{
    public const string SectionName = "BackgroundJobs:RefreshTokenCleanup";

    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 60;
}
