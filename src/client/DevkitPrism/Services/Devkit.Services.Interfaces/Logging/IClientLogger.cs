namespace Devkit.Services.Interfaces.Logging;

/// <summary>
/// Application-facing logging abstraction.
/// </summary>
public interface IClientLogger
{
    void Information(string message, params object?[] args);

    void Warning(Exception? exception, string message, params object?[] args);

    void Error(Exception exception, string message, params object?[] args);

    void Critical(Exception exception, string message, params object?[] args);
}
