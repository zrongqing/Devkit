using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using Barcode2.Core.ApiCodeCollector;
using Barcode2.DB.Context;

namespace Barcode2.Servers;

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

    public string GetSourceCodeByApiName(string sourcePath, string apiName)
    {
        var sourceCode = _apiCollector.GetApiExtendCodeByName(sourcePath, apiName);
        return FormatSourceCode(sourceCode);
    }

    public string GetSourceCodeByApiName(List<ApiSourceInfo> apiSourceInfos, string apiName)
    {
        var sourceCode = _apiCollector.GetApiExtendCodeByName(apiSourceInfos, apiName);
        return FormatSourceCode(sourceCode);
    }

    public string GetExecutionSourceCodeByApiCode(string sourcePath, string apiCode)
    {
        var sourceCode = _apiScanner.GetExecutionSourceCode(sourcePath, apiCode);
        var formattedSourceCode = FormatMethodBodySourceCode(sourceCode);
        return IndentSourceCode(formattedSourceCode);
    }

    public string GetExecutionSourceCodeByApiName(string sourcePath, string apiName)
    {
        var sourceCode = _apiScanner.GetExecutionSourceCodeByName(sourcePath, apiName);
        var formattedSourceCode = FormatMethodBodySourceCode(sourceCode);
        return IndentSourceCode(formattedSourceCode);
    }

    public bool UpdateExtendCode(string apiCode, string extendCode, string connectionString)
    {
        return UpdateExtendBatch(
            [new ApiExtendUpdateRequest(ApiLookupKind.Code, apiCode, extendCode)],
            connectionString);
    }

    public bool UpdateExtendName(string apiName, string extendCode, string connectionString)
    {
        return UpdateExtendBatch(
            [new ApiExtendUpdateRequest(ApiLookupKind.Name, apiName, extendCode)],
            connectionString);
    }

    public bool UpdateExtendBatch(
        IReadOnlyCollection<ApiExtendUpdateRequest> updates,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(updates);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        using var db = CreateDbContext(connectionString);
        using var transaction = db.Database.BeginTransaction();
        try
        {
            var targets = ResolveUpdateTargets(db, updates);
            var updatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            foreach (var target in targets)
            {
                var detailCount = db.SYS_PAGE_EVENT_CODE
                    .Where(entity => entity.ID == target.DetailId)
                    .ExecuteUpdate(setters => setters
                        .SetProperty(entity => entity.STR_EXTEND, target.Request.ExtendCode)
                        .SetProperty(entity => entity.DT_UP, updatedAt));
                var eventCount = db.SYS_PAGE_EVENT
                    .Where(entity => entity.ID == target.EventId)
                    .ExecuteUpdate(setters => setters
                        .SetProperty(entity => entity.DT_UP, updatedAt));

                if (detailCount != 1 || eventCount != 1)
                {
                    throw CreateDataMatchException(
                        target.Request,
                        $"更新期间主副表记录数发生变化（主表 {eventCount} 条，副表 {detailCount} 条）");
                }
            }

            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
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

    protected virtual MyDbContext CreateDbContext(string connectionString) => new(connectionString);

    private static List<ResolvedUpdateTarget> ResolveUpdateTargets(
        MyDbContext db,
        IReadOnlyCollection<ApiExtendUpdateRequest> updates)
    {
        var targets = new List<ResolvedUpdateTarget>();
        foreach (var request in updates)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(request.Identifier);

            var eventIds = db.SYS_PAGE_EVENT
                .Where(entity => request.LookupKind == ApiLookupKind.Code
                                     ? entity.STR_CODE == request.Identifier
                                     : entity.STR_NAME == request.Identifier)
                .Select(entity => entity.ID)
                .ToList();

            if (eventIds.Count > 1)
            {
                throw CreateDataMatchException(request, $"主表命中 {eventIds.Count} 条记录");
            }

            if (eventIds.Count == 0)
            {
                continue;
            }

            var eventId = eventIds[0];
            var detailIds = db.SYS_PAGE_EVENT_CODE
                .Where(entity => entity.ID_EVENT == eventId)
                .Select(entity => entity.ID)
                .ToList();
            if (detailIds.Count != 1)
            {
                throw CreateDataMatchException(request, $"关联副表命中 {detailIds.Count} 条记录");
            }

            targets.Add(new ResolvedUpdateTarget(request, eventId, detailIds[0]));
        }

        return targets;
    }

    private static InvalidOperationException CreateDataMatchException(
        ApiExtendUpdateRequest request,
        string detail)
    {
        var lookupName = request.LookupKind == ApiLookupKind.Code ? "Code" : "Name";
        return new InvalidOperationException($"Api{lookupName}“{request.Identifier}”数据异常：{detail}，已回滚全部更新。");
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

    private sealed record ResolvedUpdateTarget(
        ApiExtendUpdateRequest Request,
        long? EventId,
        long DetailId);
}
