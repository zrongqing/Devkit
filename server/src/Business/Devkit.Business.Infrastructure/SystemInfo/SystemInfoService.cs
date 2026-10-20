using Devkit.Business.Application.Abstractions;
using Devkit.Business.Application.Contracts;

namespace Devkit.Business.Infrastructure.SystemInfo;

public sealed class SystemInfoService(TimeProvider timeProvider, ServerRuntimeOptions options) : ISystemInfoService
{
    public SystemRuntimeResponse GetRuntime()
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        var started = new DateTimeOffset(process.StartTime.ToUniversalTime());
        return new(GetInfo(), started, Math.Max(0, (timeProvider.GetUtcNow() - started).TotalSeconds),
            process.WorkingSet64, GC.GetTotalMemory(false), process.Threads.Count,
            Environment.ProcessorCount, process.TotalProcessorTime.TotalSeconds);
    }

    public SystemInfoResponse GetInfo() => new(
        options.ServiceName,
        options.Version,
        options.EnvironmentName,
        timeProvider.GetUtcNow());
}
