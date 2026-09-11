using System.Globalization;
using System.IO;
using Serilog.Core;
using Serilog.Events;

namespace Devkit.Services.Logging;

/// <summary>
/// Lightweight daily rolling file sink to keep client crash diagnostics locally.
/// </summary>
public sealed class RollingFileLogEventSink(string logDirectory) : ILogEventSink
{
    private readonly Lock _writeLock = new();

    public void Emit(LogEvent logEvent)
    {
        try
        {
            Directory.CreateDirectory(logDirectory);
            var logFile = Path.Combine(logDirectory, $"devkit-{logEvent.Timestamp:yyyyMMdd}.log");
            var exception = logEvent.Exception is null ? string.Empty : $"{Environment.NewLine}{logEvent.Exception}";
            var entry = $"{logEvent.Timestamp:O} [{logEvent.Level}] {logEvent.RenderMessage(CultureInfo.InvariantCulture)}{exception}{Environment.NewLine}";

            lock (_writeLock)
            {
                File.AppendAllText(logFile, entry);
            }
        }
        catch
        {
            // Logging must never introduce another application failure.
        }
    }
}
