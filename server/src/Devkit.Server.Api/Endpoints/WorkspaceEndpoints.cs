using Devkit.Server.Api.Contracts;
using Devkit.Server.Application.Workspace;
using Devkit.Server.Domain.Abstractions;
using Devkit.Server.Domain.Workspace;
using Devkit.Server.Infrastructure.Workspace;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Devkit.Server.Api.Endpoints;

public sealed class WorkspaceExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context,Exception exception,CancellationToken ct)
    {
        var error=exception switch
        {
            BusinessException b=>b,
            DbUpdateConcurrencyException=>new BusinessException(409,"revision_conflict","内容已变更，请刷新后重试。"),
            System.Text.Json.JsonException=>new BusinessException(400,"invalid_json","数据格式不正确。"),
            HttpRequestException=>new BusinessException(503,"dependency_unavailable","外部服务暂时不可访问，请检查配置后重试。"),
            OperationCanceledException when !ct.IsCancellationRequested=>new BusinessException(504,"operation_timeout","处理超时，请稍后重试。"),
            _=>null
        };
        if(error is null)return false;
        await Results.Problem(statusCode:error.Status,title:error.Message,extensions:new Dictionary<string,object?>{{"code",error.Code},{"traceId",context.TraceIdentifier}}).ExecuteAsync(context);return true;
    }
}

public sealed record StatusRequest(string Status);
public sealed record MasterRequest(bool Mastered);
public sealed class WorkspaceEndpoints : IEndpointModule
{
    private static async Task<Ok<ApiResponse<T>>> Run<T>(HttpContext c,Func<Actor,Task<T>> operation)
    {var actor=await c.RequestServices.GetRequiredService<IWorkspaceAccess>().CurrentAsync(c.RequestAborted);return TypedResults.Ok(new ApiResponse<T>(await operation(actor),c.TraceIdentifier));}
    private static Task<Ok<ApiResponse<SuccessView>>> Run(HttpContext c,Func<Actor,Task> operation)=>Run(c,async actor=>{await operation(actor);return new SuccessView();});
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group=endpoints.MapGroup("/api/v1/exam-study").RequireAuthorization().WithTags("Exam Study").AddEndpointFilter<StudyPermissionFilter>();
        group.MapGet("/knowledge-bases",(HttpContext c,StudyService s,bool all=false)=>Run(c,a=>s.BasesAsync(a,all,c.RequestAborted))).Produces<ApiResponse<IReadOnlyList<KnowledgeBase>>>();
        group.MapPost("/knowledge-bases",(HttpContext c,StudyService s,NamedRequest r)=>Run(c,a=>s.SaveBaseAsync(a,null,r,c.RequestAborted)));
        group.MapPut("/knowledge-bases/{id:guid}",(HttpContext c,StudyService s,Guid id,NamedRequest r)=>Run(c,a=>s.SaveBaseAsync(a,id,r,c.RequestAborted)));
        group.MapDelete("/knowledge-bases/{id:guid}",(HttpContext c,StudyService s,Guid id)=>Run(c,a=>s.DeleteBaseAsync(a,id,c.RequestAborted)));
        group.MapGet("/knowledge-bases/{id:guid}/sources",(HttpContext c,StudyService s,Guid id)=>Run(c,a=>s.SourcesAsync(a,id,c.RequestAborted)));
        group.MapPost("/knowledge-bases/{id:guid}/sources",(HttpContext c,StudyService s,Guid id,SourceRequest r)=>Accepted(c,a=>s.SaveSourceAsync(a,id,null,r,c.RequestAborted)));
        group.MapPut("/knowledge-bases/{id:guid}/sources/{sourceId:guid}",(HttpContext c,StudyService s,Guid id,Guid sourceId,SourceRequest r)=>Accepted(c,a=>s.SaveSourceAsync(a,id,sourceId,r,c.RequestAborted)));
        group.MapGet("/sources/{id:guid}",(HttpContext c,StudyService s,Guid id)=>Run(c,a=>s.SourceDetailAsync(a,id,c.RequestAborted)));
        group.MapDelete("/sources/{id:guid}",(HttpContext c,StudyService s,Guid id)=>Run(c,a=>s.DeleteSourceAsync(a,id,c.RequestAborted)));
        group.MapPost("/sources/{id:guid}/reindex",(HttpContext c,StudyService s,Guid id)=>Accepted(c,a=>s.ReindexAsync(a,id,c.RequestAborted)));
        group.MapGet("/projects",(HttpContext c,StudyService s,bool all=false)=>Run(c,a=>s.ProjectsAsync(a,all,c.RequestAborted)));
        group.MapPost("/projects",(HttpContext c,StudyService s,ProjectRequest r)=>Run(c,a=>s.SaveProjectAsync(a,null,r,c.RequestAborted)));
        group.MapPut("/projects/{id:guid}",(HttpContext c,StudyService s,Guid id,ProjectRequest r)=>Run(c,a=>s.SaveProjectAsync(a,id,r,c.RequestAborted)));
        group.MapPost("/projects/{id:guid}/search",(HttpContext c,StudyService s,Guid id,SearchRequest r)=>Run(c,a=>s.SearchAsync(a,id,r,c.RequestAborted))).Produces<ApiResponse<SearchResponse>>();
        group.MapPost("/projects/{id:guid}/ask",(HttpContext c,StudyService s,Guid id,SearchRequest r)=>Run(c,a=>s.AskAsync(a,id,r,c.RequestAborted)));
        group.MapGet("/projects/{id:guid}/history",(HttpContext c,StudyService s,Guid id)=>Run(c,a=>s.HistoryAsync(a,id,c.RequestAborted)));
        group.MapGet("/projects/{id:guid}/progress",(HttpContext c,StudyService s,Guid id)=>Run(c,a=>s.ProgressAsync(a,id,c.RequestAborted)));
        group.MapPost("/projects/{id:guid}/attempts",(HttpContext c,StudyService s,Guid id,AttemptRequest r)=>Run(c,a=>s.StartAttemptAsync(a,id,r,c.RequestAborted)));
        group.MapGet("/attempts/{id:guid}",(HttpContext c,StudyService s,Guid id)=>Run(c,a=>s.AttemptAsync(a,id,c.RequestAborted))).Produces<ApiResponse<AttemptView>>();
        group.MapPut("/attempts/{id:guid}/answers/{questionId:guid}",(HttpContext c,StudyService s,Guid id,Guid questionId,AnswerRequest r)=>Run(c,a=>s.AnswerAsync(a,id,questionId,r,c.RequestAborted)));
        group.MapPost("/attempts/{id:guid}/submit",(HttpContext c,StudyService s,Guid id)=>Run(c,a=>s.SubmitAsync(a,id,c.RequestAborted)));
        group.MapPut("/mistakes/{id:guid}",(HttpContext c,StudyService s,Guid id,MasterRequest r)=>Run(c,a=>s.MasterAsync(a,id,r.Mastered,c.RequestAborted)));
        group.MapGet("/questions",(HttpContext c,StudyService s,Guid? knowledgeBaseId,bool all=false)=>Run(c,a=>s.QuestionsAsync(a,knowledgeBaseId,all,c.RequestAborted)));
        group.MapPost("/questions",(HttpContext c,StudyService s,QuestionRequest r)=>Run(c,a=>s.SaveQuestionAsync(a,null,r,c.RequestAborted)));
        group.MapPut("/questions/{id:guid}",(HttpContext c,StudyService s,Guid id,QuestionRequest r)=>Run(c,a=>s.SaveQuestionAsync(a,id,r,c.RequestAborted)));
        group.MapPut("/questions/{id:guid}/status",(HttpContext c,StudyService s,Guid id,StatusRequest r)=>Run(c,a=>s.QuestionStatusAsync(a,id,r.Status,c.RequestAborted)));
        group.MapPost("/generation-jobs",(HttpContext c,StudyService s,GenerationRequest r)=>Accepted(c,a=>s.GenerateAsync(a,r,c.RequestAborted)));
        group.MapGet("/jobs",(HttpContext c,StudyService s)=>Run(c,a=>s.JobsAsync(a,c.RequestAborted)));
        group.MapPost("/jobs/{id:guid}/retry",(HttpContext c,StudyService s,Guid id)=>Run(c,a=>s.RetryAsync(a,id,c.RequestAborted)));
        group.MapGet("/capabilities",(HttpContext c,IModelGateway m,IKnowledgeIndex i)=>Run(c,async a=>{a.Require("exam-study.access");return new CapabilityView(m.ChatConfigured,m.EmbeddingConfigured,await i.AvailableAsync(c.RequestAborted));}));

        var file=endpoints.MapGroup("/api/v1/files").RequireAuthorization().WithTags("Files");
        file.MapGet("/",(HttpContext c,IFileService s)=>Run(c,a=>s.ListAsync(a,c.RequestAborted)));
        file.MapPost("/",async(HttpContext c,IFileService s,IFormFile file,string purpose="exam-study")=>
        {await using var stream=file.OpenReadStream();return await Run(c,a=>s.UploadAsync(a,file.FileName,purpose,stream,c.RequestAborted));}).DisableAntiforgery().Produces<ApiResponse<FileView>>();
        file.MapGet("/{id:guid}/content",async(HttpContext c,IFileService s,IWorkspaceAccess access,Guid id)=>
        {var f=await s.OpenAsync(await access.CurrentAsync(c.RequestAborted),id,c.RequestAborted);c.Response.Headers.XContentTypeOptions="nosniff";return Results.Stream(f.Stream,f.ContentType,f.Name,enableRangeProcessing:true);});
        file.MapDelete("/{id:guid}",(HttpContext c,IFileService s,Guid id)=>Run(c,a=>s.PurgeAsync(a,id,c.RequestAborted)));
        file.MapGet("/{id:guid}/references",(HttpContext c,IFileService s,Guid id)=>Run(c,a=>s.ReferencesAsync(a,id,c.RequestAborted)));

        var storage=endpoints.MapGroup("/api/v1/storage").RequireAuthorization().WithTags("Storage");
        storage.MapGet("/locations",(HttpContext c,IFileService s)=>Run(c,a=>s.LocationsAsync(a,c.RequestAborted)));
        storage.MapPost("/locations",(HttpContext c,IFileService s,LocationRequest r)=>Run(c,a=>s.AddLocationAsync(a,r,c.RequestAborted)));
        storage.MapPost("/migrations",async(HttpContext c,IFileService s,IWorkspaceAccess access,MigrationRequest r)=>
        {var id=await s.MigrateAsync(await access.CurrentAsync(c.RequestAborted),r,c.RequestAborted);return TypedResults.Accepted("/api/v1/exam-study/jobs",new ApiResponse<JobCreatedView>(new(id),c.TraceIdentifier));});
        storage.MapPost("/migrations/{id:guid}/cleanup",(HttpContext c,IFileService s,Guid id)=>Run(c,a=>s.CleanupAsync(a,id,c.RequestAborted)));

        var identity=endpoints.MapGroup("/api/v1/identity").RequireAuthorization().WithTags("Identity Administration");
        identity.MapGet("/users",(HttpContext c,IAccountAdministration s)=>Run(c,a=>s.UsersAsync(a,c.RequestAborted)));
        identity.MapGet("/permissions", (HttpContext c, IWorkspaceAccess access) => Run(c, a => { a.RequireAny("system.users.manage", "system.roles.manage", "system.permissions.manage"); return Task.FromResult(PermissionCatalog.Modules); }));
        identity.MapPut("/users/{id:guid}", (HttpContext c, IAccountAdministration s, Guid id, AccountUpdateRequest r) => Run(c, a => s.UpdateAsync(a, id, r, c.RequestAborted)));
        identity.MapPut("/users/{id:guid}/permissions", (HttpContext c, IAccountAdministration s, Guid id, AssignPermissionsRequest r) => Run(c, a => s.AssignPermissionsAsync(a, id, r.Permissions, c.RequestAborted)));
        identity.MapPost("/users",(HttpContext c,IAccountAdministration s,AccountRequest r)=>Run(c,a=>s.CreateAsync(a,r,c.RequestAborted)));
        identity.MapDelete("/users/{id:guid}",(HttpContext c,IAccountAdministration s,Guid id)=>Run(c,a=>s.DisableAsync(a,id,c.RequestAborted)));
        identity.MapGet("/roles",(HttpContext c,IAccountAdministration s)=>Run(c,a=>s.RolesAsync(a,c.RequestAborted)));
        identity.MapPost("/roles",(HttpContext c,IAccountAdministration s,RoleRequest r)=>Run(c,a=>s.SaveRoleAsync(a,null,r,c.RequestAborted)));
        identity.MapPut("/roles/{id:guid}",(HttpContext c,IAccountAdministration s,Guid id,RoleRequest r)=>Run(c,a=>s.SaveRoleAsync(a,id,r,c.RequestAborted)));
        identity.MapPut("/users/{id:guid}/roles",(HttpContext c,IAccountAdministration s,Guid id,AssignRolesRequest r)=>Run(c,a=>s.AssignAsync(a,id,r.RoleIds,c.RequestAborted)));
        endpoints.MapGet("/api/v1/auth/permissions",(HttpContext c,IWorkspaceAccess access)=>Run(c,a=>Task.FromResult(a))).RequireAuthorization().WithTags("Authentication");
        endpoints.MapPost("/api/v1/auth/password",(HttpContext c,IAccountAdministration s,ChangePasswordRequest r)=>Run(c,a=>s.ChangePasswordAsync(a,r,c.RequestAborted))).RequireAuthorization().WithTags("Authentication");
    }
    private static async Task<Accepted<ApiResponse<WorkJob>>> Accepted(HttpContext c,Func<Actor,Task<WorkJob>> operation)
    {var a=await c.RequestServices.GetRequiredService<IWorkspaceAccess>().CurrentAsync(c.RequestAborted);var job=await operation(a);return TypedResults.Accepted("/api/v1/exam-study/jobs",new ApiResponse<WorkJob>(job,c.TraceIdentifier));}
}
