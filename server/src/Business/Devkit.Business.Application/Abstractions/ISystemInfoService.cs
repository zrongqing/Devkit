using Devkit.Commons.Runtime;

namespace Devkit.Business.Application.Abstractions;

public interface ISystemInfoService
{
    SystemInfoResponse GetInfo();
    SystemRuntimeResponse GetRuntime();
}
