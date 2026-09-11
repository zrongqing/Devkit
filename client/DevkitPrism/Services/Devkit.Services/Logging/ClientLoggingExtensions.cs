using Devkit.Services.Interfaces.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Devkit.Services.Logging;

/// <summary>
/// Registers Serilog-backed client logging and creates the logger needed before DI starts.
/// </summary>
public static class ClientLoggingExtensions
{
    public static IServiceCollection AddClientLogging(this IServiceCollection services)
    {
        services.AddLogging(builder => builder.AddSerilog(CreateSerilogLogger(), dispose: true));
        services.AddSingleton<IClientLogger, ClientLogger>();
        return services;
    }

    public static ILoggerFactory CreateBootstrapLoggerFactory() =>
        LoggerFactory.Create(builder => builder.AddSerilog(CreateSerilogLogger(), dispose: true));

    private static Serilog.ILogger CreateSerilogLogger()
    {
        var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");

        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Sink(new RollingFileLogEventSink(logDirectory))
            .CreateLogger();
    }
}
