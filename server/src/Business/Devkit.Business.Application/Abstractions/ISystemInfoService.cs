using Devkit.Business.Application.Contracts;

namespace Devkit.Business.Application.Abstractions;

public interface ISystemInfoService
{
    SystemInfoResponse GetInfo();
    SystemRuntimeResponse GetRuntime();
}
