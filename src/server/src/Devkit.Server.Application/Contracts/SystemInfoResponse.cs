namespace Devkit.Server.Application.Contracts;

public sealed record SystemInfoResponse(
    string ServiceName,
    string Version,
    string Environment,
    DateTimeOffset ServerTime);
