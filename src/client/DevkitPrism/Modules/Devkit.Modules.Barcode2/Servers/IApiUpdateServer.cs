using Barcode2.Core.ApiCodeCollector;

namespace Barcode2.Servers;

public interface IApiUpdateServer
{
    List<ApiSourceInfo> GetAllApiSourceInfos(string sourcePath);

    List<string> GetAllApiCodes(List<ApiSourceInfo> sourceInfos);

    string GetSourceCodeByApiCode(string sourcePath, string apiCode);

    string GetSourceCodeByApiCode(List<ApiSourceInfo> apiSourceInfos, string apiCode);

    string GetSourceCodeByApiName(string sourcePath, string apiName);

    string GetSourceCodeByApiName(List<ApiSourceInfo> apiSourceInfos, string apiName);

    string GetExecutionSourceCodeByApiCode(string sourcePath, string apiCode);

    string GetExecutionSourceCodeByApiName(string sourcePath, string apiName);

    bool UpdateExtendCode(string apiCode, string extendCode, string connectionString);

    bool UpdateExtendName(string apiName, string extendCode, string connectionString);

    bool UpdateExtendBatch(
        IReadOnlyCollection<ApiExtendUpdateRequest> updates,
        string connectionString);

    bool UpdateSourceCode(string apiCode, string sourceCode, string connectionString);
}
