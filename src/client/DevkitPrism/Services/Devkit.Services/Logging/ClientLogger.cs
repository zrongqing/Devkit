using Devkit.Services.Interfaces.Logging;
using Microsoft.Extensions.Logging;

namespace Devkit.Services.Logging;

public sealed class ClientLogger(ILogger<ClientLogger> logger) : IClientLogger
{
    public void Information(string message, params object?[] args) =>
        logger.LogInformation(message, args);

    public void Warning(Exception? exception, string message, params object?[] args) =>
        logger.LogWarning(exception, message, args);

    public void Error(Exception exception, string message, params object?[] args) =>
        logger.LogError(exception, message, args);

    public void Critical(Exception exception, string message, params object?[] args) =>
        logger.LogCritical(exception, message, args);
}
