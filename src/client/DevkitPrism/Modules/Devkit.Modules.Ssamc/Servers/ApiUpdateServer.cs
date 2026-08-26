using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using Ssamc.Core.ApiCodeCollector;
using Ssamc.DB.Context;

namespace Ssamc.Servers;

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

    #region IApiUpdateServer Members

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

    public string GetExecutionSourceCodeByApiCode(string sourcePath, string apiCode)
    {
        var sourceCode = _apiScanner.GetExecutionSourceCode(sourcePath, apiCode);
        var formattedSourceCode = FormatMethodBodySourceCode(sourceCode);
        return IndentSourceCode(formattedSourceCode);
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

    #endregion

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
                .NormalizeWhitespace("    ", Environment.NewLine)
                .ToFullString();
        }
        catch (Exception)
        {
            return sourceCode;
        }
    }

    private static string FormatMethodBodySourceCode(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return string.Empty;
        }

        try
        {
            var block = SyntaxFactory.ParseStatement($"{{{Environment.NewLine}{sourceCode}{Environment.NewLine}}}")
                as BlockSyntax;
            if (block is null)
            {
                return sourceCode;
            }

            var formattedLines = block
                .NormalizeWhitespace("    ", Environment.NewLine)
                .ToFullString()
                .Split(Environment.NewLine);

            var bodyLines = formattedLines[1..^1]
                .Select(line => line.StartsWith("    ", StringComparison.Ordinal) ? line[4..] : line);
            return string.Join(Environment.NewLine, bodyLines);
        }
        catch (Exception)
        {
            return sourceCode;
        }
    }

    private static string IndentSourceCode(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            sourceCode.Split(Environment.NewLine)
                .Select(line => string.IsNullOrWhiteSpace(line) ? string.Empty : $"    {line}"));
    }
}
