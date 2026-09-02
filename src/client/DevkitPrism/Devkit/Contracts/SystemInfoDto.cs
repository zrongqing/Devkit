namespace Devkit.Contracts;

public sealed record SystemInfoDto(string ServiceName, string Version, string Environment, DateTimeOffset ServerTime);
