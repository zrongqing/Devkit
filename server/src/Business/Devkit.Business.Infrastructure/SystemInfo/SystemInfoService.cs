using Devkit.Business.Application.Abstractions;
using Devkit.Commons.Runtime;

namespace Devkit.Business.Infrastructure.SystemInfo;

public sealed class SystemInfoService(TimeProvider timeProvider, ServerRuntimeOptions options) : ISystemInfoService
{
    public SystemRuntimeResponse GetRuntime() => SystemRuntimeResponse.Sample(GetInfo());

    public SystemInfoResponse GetInfo() => new(
        options.ServiceName,
        options.Version,
        options.EnvironmentName,
        timeProvider.GetUtcNow());
}
