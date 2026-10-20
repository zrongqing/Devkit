namespace Devkit.Commons.Exceptions;

// Context-specific exceptions retain their names and supply their own codes and messages.
public abstract class ServiceException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
