namespace Devkit.Modules.Ssamc.Core.ApiCodeCollector;

/// <summary>
/// API扫描器接口
/// </summary>
public interface IApiScanner
{
    /// <summary>
    /// 扫描指定路径下的所有源代码文件
    /// </summary>
    /// <param name="sourcePath">源代码路径</param>
    /// <param name="searchPattern">搜索模式，默认*.cs</param>
    /// <returns>API信息列表</returns>
    List<ApiSourceInfo> ScanSourceFiles(string sourcePath, string searchPattern = "*.cs");

    /// <summary>
    /// 获取由 <c>ApiSourceCode</c> 标记的执行源代码。
    /// </summary>
    /// <param name="sourcePath">源代码路径</param>
    /// <param name="apiCode">API 编码</param>
    /// <returns>匹配方法的方法体代码</returns>
    string GetExecutionSourceCode(string sourcePath, string apiCode);

    /// <summary>
    /// 根据ApiCode将源代码写入到对应文件中
    /// </summary>
    /// <param name="apiInfos">API信息列表</param>
    /// <param name="outputDirectory">输出目录</param>
    void WriteSourceCodeByApiCode(List<ApiSourceInfo> apiInfos, string outputDirectory);
}
