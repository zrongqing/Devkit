namespace Devkit.Server.Infrastructure.Configuration;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = "localhost:6379,abortConnect=false";
    public string InstanceName { get; set; } = "devkit:";
    public int ProfileTtlMinutes { get; set; } = 5;
}
