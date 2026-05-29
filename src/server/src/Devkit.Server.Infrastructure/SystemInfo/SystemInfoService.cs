using Devkit.Server.Application.Abstractions;
using Devkit.Server.Application.Contracts;

namespace Devkit.Server.Infrastructure.SystemInfo;

public sealed class SystemInfoService(TimeProvider timeProvider, ServerRuntimeOptions options) : ISystemInfoService
{
    public SystemInfoResponse GetInfo() => new(
        options.ServiceName,
        options.Version,
        options.EnvironmentName,
        timeProvider.GetUtcNow());
}
