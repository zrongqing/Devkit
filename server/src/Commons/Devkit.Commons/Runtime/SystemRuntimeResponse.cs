namespace Devkit.Commons.Runtime;

public sealed record SystemInfoResponse(string ServiceName, string Version, string Environment, DateTimeOffset ServerTime);

public sealed record SystemRuntimeResponse(
    SystemInfoResponse Info, DateTimeOffset StartedAtUtc, double UptimeSeconds, long WorkingSetBytes,
    long ManagedMemoryBytes, int ThreadCount, int ProcessorCount, double TotalProcessorSeconds)
{
    public static SystemRuntimeResponse Sample(SystemInfoResponse info)
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        var started = new DateTimeOffset(process.StartTime.ToUniversalTime());
        return new(info, started, Math.Max(0, (info.ServerTime - started).TotalSeconds),
            process.WorkingSet64, GC.GetTotalMemory(false), process.Threads.Count,
            Environment.ProcessorCount, process.TotalProcessorTime.TotalSeconds);
    }
}
