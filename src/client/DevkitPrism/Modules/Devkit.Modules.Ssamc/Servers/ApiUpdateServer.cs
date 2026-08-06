using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.EntityFrameworkCore;
using Ssamc.Core.ApiCodeCollector;
using SSAMC.DB;

namespace Module.Ssamc.Servers;

public class ApiUpdateServer : IApiUpdateServer
{
    private readonly ApiCollector _apiCollector;
    private readonly IApiScanner _apiScanner;

    public ApiUpdateServer(IApiScanner apiScanner)
    {
        _apiScanner = apiScanner;
        _apiCollector = new ApiCollector(apiScanner);
    }

    public List<ApiSourceInfo> CopyApiSourceInfos { get; set; } = [];

    public List<ApiSourceInfo> GetAllApiSourceInfos(string sourcePath)
    {
        var apiInfos = _apiScanner.ScanSourceFiles(sourcePath);
        CopyApiSourceInfos = apiInfos;
        return apiInfos;
    }

    public List<string> GetAllApiCodes(List<ApiSourceInfo> sourceInfos)
    {
        var apiCodes = new HashSet<string>();
        foreach (var sourceInfo in sourceInfos)
        {
            foreach (var apiCode in sourceInfo.ApiCodes)
            {
                apiCodes.Add(apiCode);
            }
        }

        return apiCodes.ToList();
    }

    public string GetSourceCodeByApiCode(string sourcePath, string apiCode)
    {
        var sourceCode = _apiCollector.GetApiExtendCode(sourcePath, apiCode);
        return FormatSourceCode(sourceCode);
    }

    public string GetSourceCodeByApiCode(List<ApiSourceInfo> apiSourceInfos, string apiCode)
    {
        var sourceCode = _apiCollector.GetApiExtendCode(apiSourceInfos, apiCode);
        return FormatSourceCode(sourceCode);
    }

    private static string FormatSourceCode(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return string.Empty;
        }

        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            return syntaxTree.GetRoot()
                .NormalizeWhitespace(indentation: "    ", eol: Environment.NewLine)
                .ToFullString();
        }
        catch (Exception)
        {
            return sourceCode;
        }
    }

    public string GetExecutionSourceCodeByApiCode(string sourcePath, string apiCode)
    {
        return _apiScanner.GetExecutionSourceCode(sourcePath, apiCode);
    }

    public bool UpdateExtendCode(string apiCode, string extendCode, string connectionString)
    {
        using var db = new MyDbContext(connectionString);
        var updatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        db.SYS_PAGE_EVENT_CODE
            .Where(entity => entity.STR_CODE == apiCode)
            .ExecuteUpdate(setters => setters
                .SetProperty(entity => entity.STR_EXTEND, extendCode)
                .SetProperty(entity => entity.DT_UP, updatedAt));

        db.SYS_PAGE_EVENT
            .Where(entity => entity.STR_CODE == apiCode)
            .ExecuteUpdate(setters => setters
                .SetProperty(entity => entity.DT_UP, updatedAt));

        return true;
    }

    public bool UpdateSourceCode(string apiCode, string sourceCode, string connectionString)
    {
        using var db = new MyDbContext(connectionString);
        var eventCode = db.SYS_PAGE_EVENT_CODE
            .Include(entity => entity.SYS_PAGE_EVENT)
            .Where(entity => entity.SYS_PAGE_EVENT != null)
            .Where(entity => entity.SYS_PAGE_EVENT!.STR_CODE == apiCode)
            .FirstOrDefault(entity => entity.SYS_PAGE_EVENT!.STR_CLASS == "api");

        if (eventCode is null)
        {
            return false;
        }

        eventCode.STR_SOURCE = sourceCode;
        db.SaveChanges();
        return true;
    }
}
