using Devkit.Modules.Ssamc.Core.ApiCodeCollector;

namespace Devkit.Modules.Ssamc.Servers;

public interface IApiUpdateServer
{
    List<ApiSourceInfo> GetAllApiSourceInfos(string sourcePath);

    List<string> GetAllApiCodes(List<ApiSourceInfo> sourceInfos);

    string GetSourceCodeByApiCode(string sourcePath, string apiCode);

    string GetSourceCodeByApiCode(List<ApiSourceInfo> apiSourceInfos, string apiCode);

    string GetExecutionSourceCodeByApiCode(string sourcePath, string apiCode);

    bool UpdateExtendCode(string apiCode, string extendCode, string connectionString);

    bool UpdateSourceCode(string apiCode, string sourceCode, string connectionString);
}
