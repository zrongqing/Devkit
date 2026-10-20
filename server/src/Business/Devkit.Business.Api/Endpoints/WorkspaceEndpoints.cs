using Devkit.Business.Api.Contracts;
using Devkit.Business.Application.Workspace;
using Devkit.Business.Domain.Abstractions;
using Devkit.Business.Domain.Workspace;
using Devkit.Business.Infrastructure.Workspace;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Devkit.Business.Api.Endpoints;

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




    }
    private static async Task<Accepted<ApiResponse<WorkJob>>> Accepted(HttpContext c,Func<Actor,Task<WorkJob>> operation)
    {var a=await c.RequestServices.GetRequiredService<IWorkspaceAccess>().CurrentAsync(c.RequestAborted);var job=await operation(a);return TypedResults.Accepted("/api/v1/exam-study/jobs",new ApiResponse<WorkJob>(job,c.TraceIdentifier));}
}
