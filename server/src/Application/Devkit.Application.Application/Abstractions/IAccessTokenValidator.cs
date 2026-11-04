namespace Devkit.Application.Application.Abstractions;

public interface IAccessTokenValidator
{
    Task<bool> IsCurrentAsync(Guid userId, int authenticationVersion, CancellationToken cancellationToken);
}
