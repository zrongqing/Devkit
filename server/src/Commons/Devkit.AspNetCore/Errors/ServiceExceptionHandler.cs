using System.Text.Json;
using Devkit.Commons.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Devkit.AspNetCore.Errors;

public sealed class ServiceExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        (int Status, string Code, string Message)? failure = exception switch
        {
            ServiceException service => (service.Status, service.Code, service.Message),
            DbUpdateConcurrencyException => (409, "revision_conflict", "内容已变更，请刷新后重试。"),
            JsonException => (400, "invalid_json", "数据格式不正确。"),
            HttpRequestException => (503, "dependency_unavailable", "外部服务暂时不可访问，请检查配置后重试。"),
            OperationCanceledException when !cancellationToken.IsCancellationRequested => (504, "operation_timeout", "处理超时，请稍后重试。"),
            _ => null
        };
        if (failure is not { } error) return false;
        await Results.Problem(statusCode: error.Status, title: error.Message,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code, ["traceId"] = context.TraceIdentifier })
            .ExecuteAsync(context);
        return true;
    }
}
