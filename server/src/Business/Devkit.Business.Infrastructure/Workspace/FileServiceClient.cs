using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Devkit.Business.Application.Workspace;
using Devkit.Business.Domain.Workspace;
using Devkit.Business.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Devkit.Business.Infrastructure.Workspace;

public sealed class FileServiceOptions
{
    public string BaseUrl { get; set; } = "https://localhost:12513";
    public bool RequireHttps { get; set; } = true;
    public string ClientName { get; set; } = "Business";
    public string ApiKey { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 60;
    public long MaximumDownloadBytes { get; set; } = 100 * 1024 * 1024;
}
public static class FileServiceRegistration
{
    public static IServiceCollection AddFileServiceClient(this IServiceCollection services,IConfiguration config)
    {
        services.AddOptions<FileServiceOptions>().Bind(config.GetSection("FileService"))
            .Validate(o => Uri.TryCreate(o.BaseUrl,UriKind.Absolute,out var uri)
                && uri.Scheme is "https" or "http" && (!o.RequireHttps || uri.Scheme=="https")
                && uri.AbsolutePath=="/" && uri.Query.Length==0 && uri.Fragment.Length==0 && uri.UserInfo.Length==0,
                "FileService requires a root HTTPS URL unless RequireHttps is false")
            .Validate(o=>System.Text.Encoding.UTF8.GetByteCount(o.ApiKey)>=32 && !string.IsNullOrWhiteSpace(o.ClientName),"FileService client credentials are required (key >= 32 UTF-8 bytes)")
            .Validate(o=>o.TimeoutSeconds is >0 and <=900 && o.MaximumDownloadBytes is >0 and <=1024L*1024*1024,"Invalid file service limits")
            .ValidateOnStart();
        services.AddHttpClient("files",(provider,client)=>
        {
            var o=provider.GetRequiredService<IOptions<FileServiceOptions>>().Value;
            client.BaseAddress=new Uri(o.BaseUrl);client.Timeout=TimeSpan.FromSeconds(o.TimeoutSeconds);
            client.DefaultRequestHeaders.Add("X-File-Service",o.ClientName);
            client.DefaultRequestHeaders.Add("X-File-Key",o.ApiKey);
        }).ConfigurePrimaryHttpMessageHandler(()=>new HttpClientHandler{AllowAutoRedirect=false});
        services.AddHealthChecks().AddCheck<FileServiceHealthCheck>("file-service",tags:["ready"]);
        return services;
    }
}
public sealed class FileServiceHealthCheck(IHttpClientFactory clients) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,CancellationToken cancellationToken=default)
    {
        try
        {
            using var client=clients.CreateClient("files");using var response=await client.GetAsync("health/ready",cancellationToken);
            return response.IsSuccessStatusCode?HealthCheckResult.Healthy():HealthCheckResult.Unhealthy("File service is not ready");
        }
        catch(Exception e) when(e is HttpRequestException or OperationCanceledException)
        {return HealthCheckResult.Unhealthy("File service is unavailable",e);}
    }
}
public sealed class RemoteFileService(IHttpClientFactory clients,IHttpContextAccessor http,IServiceScopeFactory scopes,
    IOptions<FileServiceOptions> configured) : IFileService
{
    public async Task PrepareLinkAsync(Actor actor,Guid id,Guid entityId,Guid ownerId,CancellationToken ct)
    {
        // Persist the compensation before the remote side effect, outside the source transaction.
        using(var scope=scopes.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<BusinessDbContext>();
            db.Add(new FileRelease{EntityId=entityId,KeepIfVersionExists=true,NotBeforeUtc=DateTime.UtcNow.AddDays(1)});
            await db.SaveChangesAsync(ct);
        }
        using var request=new HttpRequestMessage(HttpMethod.Put,$"api/v1/internal/files/{id}/references/{entityId}")
        {Content=JsonContent.Create(new {ownerId})};
        var bearer=http.HttpContext?.Request.Headers.Authorization.ToString();
        if(string.IsNullOrEmpty(bearer))throw new BusinessException(401,"unauthorized","请先登录。");
        request.Headers.TryAddWithoutValidation("Authorization",bearer);
        using var response=await SendAsync(request,ct);await RequireSuccess(response,ct);
    }
    public async Task CompleteLinkAsync(Guid entityId,CancellationToken ct)
    {
        try {await RemoveIntents(entityId,ct);}
        catch(System.Data.Common.DbException) { /* The recovery worker verifies the committed source version. */ }
    }
    public async Task CancelLinkAsync(Guid entityId,CancellationToken ct)
    {
        // If the service is unavailable the pre-existing intent is retained for recovery.
        try
        {
            // A failed commit acknowledgement may still mean that the source was committed.
            // Confirm absence before compensation; database outages must retain the reference.
            using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<BusinessDbContext>();
            if(!await db.Set<SourceVersion>().AnyAsync(x=>x.Id==entityId,ct))await ReleaseAsync(entityId,ct);
            await RemoveIntents(entityId,ct);
        }
        catch(BusinessException e) when(e.Status==503) { }
        catch(System.Data.Common.DbException) { }
    }
    private async Task RemoveIntents(Guid entityId,CancellationToken ct)
    {
        using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<BusinessDbContext>();
        await db.Set<FileRelease>().Where(x=>x.EntityId==entityId && x.KeepIfVersionExists).ExecuteDeleteAsync(ct);
    }
    public async Task ReleaseAsync(Guid entityId,CancellationToken ct)
    {
        using var request=new HttpRequestMessage(HttpMethod.Delete,$"api/v1/internal/files/references/{entityId}");
        using var response=await SendAsync(request,ct);await RequireSuccess(response,ct);
    }
    public async Task ReconcileAsync(CancellationToken ct)
    {
        using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<BusinessDbContext>();
        foreach(var intent in await db.Set<FileRelease>().Where(x=>x.NotBeforeUtc<=DateTime.UtcNow).OrderBy(x=>x.CreatedAtUtc).Take(50).ToListAsync(ct))
        {
            if(!intent.KeepIfVersionExists || !await db.Set<SourceVersion>().AnyAsync(x=>x.Id==intent.EntityId,ct))
                await ReleaseAsync(intent.EntityId,ct);
            db.Remove(intent);await db.SaveChangesAsync(ct);
        }
    }
    public async Task<OpenedFile> OpenInternalAsync(Guid id,Guid entityId,CancellationToken ct)
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(configured.Value.TimeoutSeconds));
        var callerCancellation=ct;ct=deadline.Token;
        using var request=new HttpRequestMessage(HttpMethod.Get,$"api/v1/internal/files/{id}/content?entityId={entityId}");
        var stream=new MemoryStream();
        try
        {
            using var response=await SendAsync(request,ct);await RequireSuccess(response,ct);
            await using var input=await response.Content.ReadAsStreamAsync(ct);var buffer=new byte[65536];int count;
            while((count=await input.ReadAsync(buffer,ct))>0)
            {
                if(stream.Length+count>configured.Value.MaximumDownloadBytes)throw new BusinessException(413,"file_too_large","文件超过业务文档处理上限。");
                await stream.WriteAsync(buffer.AsMemory(0,count),ct);
            }
            stream.Position=0;
            var disposition=response.Content.Headers.ContentDisposition;
            var name=(disposition?.FileNameStar??disposition?.FileName??"document").Trim('"');
            return new OpenedFile(stream,name,response.Content.Headers.ContentType?.MediaType??"application/octet-stream");
        }
        catch(HttpRequestException){await stream.DisposeAsync();throw Unavailable();}
        catch(OperationCanceledException) when(!callerCancellation.IsCancellationRequested){await stream.DisposeAsync();throw Unavailable();}
        catch {await stream.DisposeAsync();throw;}
    }
    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
    {
        try
        {
            using var client=clients.CreateClient("files");
            return await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
        }
        catch(HttpRequestException){throw Unavailable();}
        catch(OperationCanceledException) when(!ct.IsCancellationRequested){throw Unavailable();}
    }
    private static async Task RequireSuccess(HttpResponseMessage response,CancellationToken ct)
    {
        if(response.IsSuccessStatusCode)return;
        try
        {
            var problem=await response.Content.ReadFromJsonAsync<FileProblem>(ct);
            if(problem is {Code:not null,Title:not null} && response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden or HttpStatusCode.NotFound or HttpStatusCode.Conflict)
                throw new BusinessException((int)response.StatusCode,problem.Code,problem.Title);
            if(response.StatusCode==HttpStatusCode.Unauthorized && problem?.Code=="unauthorized")
                throw new BusinessException(401,"unauthorized","登录状态已失效。");
        }
        catch(JsonException) { }
        catch(NotSupportedException) { }
        throw Unavailable();
    }
    private static BusinessException Unavailable()=>new(503,"file_service_unavailable","文件服务暂时不可用，请稍后重试。");
    private sealed record FileProblem(string? Code,string? Title);
}
