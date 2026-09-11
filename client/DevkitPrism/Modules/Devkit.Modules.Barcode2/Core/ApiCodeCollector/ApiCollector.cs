using System.Text;

namespace Barcode2.Core.ApiCodeCollector;

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
    public string GetSourceCode(string sourcePath, string apiCode)
    {
        var apiInfos = _scanner.GetApiSourceInfos(sourcePath, apiCode);
        return GetSourceCode(apiInfos, apiCode);
    }

    public string GetSourceCode(List<ApiSourceInfo> apiInfos, string apiCode)
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

    public string GetSourceCodeByName(string sourcePath, string apiName)
    {
        var apiInfos = _scanner.GetApiSourceInfosByName(sourcePath, apiName);
        return GetSourceCodeByName(apiInfos, apiName);
    }

    public string GetSourceCodeByName(List<ApiSourceInfo> apiInfos, string apiName)
    {
        var targetInfos = apiInfos
            .Where(info => info.ApiNames.Contains(apiName, StringComparer.Ordinal))
            .ToList();

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
