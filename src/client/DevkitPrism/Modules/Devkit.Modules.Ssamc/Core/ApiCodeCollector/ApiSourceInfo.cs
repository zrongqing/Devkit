using Microsoft.CodeAnalysis;

namespace Devkit.Modules.Ssamc.Core.ApiCodeCollector;

/// <summary>
/// API源代码信息
/// </summary>
public class ApiSourceInfo
{
    public string FilePath { get; set; } = string.Empty;
    public List<string> ApiCodes { get; set; } = new List<string>(); 
    public List<string> Descriptions { get; set; } = new List<string>(); 
    public string ClassName { get; set; } = string.Empty;
    public string FullSourceCode { get; set; } = string.Empty;
    public string SourceCodeWithoutApiAttributes { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public FileLinePositionSpan LineSpan { get; set; }
    /// <summary>
    /// 获取主ApiCode（第一个）
    /// </summary>
    public string PrimaryApiCode => ApiCodes.Count > 0 ? ApiCodes[0] : string.Empty;
    /// <summary>
    /// 获取所有ApiCode的合并字符串（用于显示）
    /// </summary>
    public string ApiCodesDisplay => string.Join(", ", ApiCodes);
}