namespace Devkit.Server.Application.Contracts;

public sealed record SystemInfoResponse(
    string ServiceName,
    string Version,
    string Environment,
    DateTimeOffset ServerTime);

public sealed record SystemRuntimeResponse(
    SystemInfoResponse Info,
    DateTimeOffset StartedAtUtc,
    double UptimeSeconds,
    long WorkingSetBytes,
    long ManagedMemoryBytes,
    int ThreadCount,
    int ProcessorCount,
    double TotalProcessorSeconds);
