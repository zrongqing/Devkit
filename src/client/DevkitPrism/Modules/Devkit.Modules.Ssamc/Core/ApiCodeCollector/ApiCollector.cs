using System.Text;

namespace Ssamc.Core.ApiCodeCollector;

/// <summary>
/// API管理器
/// </summary>
public class ApiCollector
{
    private readonly IApiScanner _scanner;
    public ApiCollector(IApiScanner scanner)
    {
        _scanner = scanner;
    }

    /// <summary>
    /// 获取源代码
    /// </summary>
    public string GetApiExtendCode(string sourcePath, string apiCode)
    {
        var apiInfos = _scanner.GetApiSourceInfos(sourcePath, apiCode);
        return GetApiExtendCode(apiInfos, apiCode);
    }

    public string GetApiExtendCode(List<ApiSourceInfo> apiInfos, string apiCode)
    {
        var targetInfos = apiInfos.Where(info => info.ApiCodes.Contains(apiCode)).ToList();

        if (targetInfos.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();

        foreach (var info in targetInfos)
        {
            sb.AppendLine(info.SourceCodeWithoutApiAttributes);
        }

        return sb.ToString();
    }
}
