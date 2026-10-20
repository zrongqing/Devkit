using Devkit.Identity.Application.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace Devkit.Identity.Api.Endpoints;

public sealed class IdentityExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context,Exception exception,CancellationToken ct)
    {
        var error=exception switch
        {
            IdentityException b=>b,
            DbUpdateConcurrencyException=>new IdentityException(409,"revision_conflict","内容已变更，请刷新后重试。"),
            System.Text.Json.JsonException=>new IdentityException(400,"invalid_json","数据格式不正确。"),
            HttpRequestException=>new IdentityException(503,"dependency_unavailable","外部服务暂时不可访问，请检查配置后重试。"),
            OperationCanceledException when !ct.IsCancellationRequested=>new IdentityException(504,"operation_timeout","处理超时，请稍后重试。"),
            _=>null
        };
        if(error is null)return false;
        await Results.Problem(statusCode:error.Status,title:error.Message,extensions:new Dictionary<string,object?>{{"code",error.Code},{"traceId",context.TraceIdentifier}}).ExecuteAsync(context);return true;
    }
}

