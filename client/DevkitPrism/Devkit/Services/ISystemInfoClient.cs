using Devkit.Contracts;

namespace Devkit.Services;

public interface ISystemInfoClient
{
    Task<SystemInfoDto> GetInfoAsync(CancellationToken cancellationToken = default);
}
