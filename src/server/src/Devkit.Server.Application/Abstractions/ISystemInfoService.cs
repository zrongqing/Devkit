using Devkit.Server.Application.Contracts;

namespace Devkit.Server.Application.Abstractions;

public interface ISystemInfoService
{
    SystemInfoResponse GetInfo();
}
