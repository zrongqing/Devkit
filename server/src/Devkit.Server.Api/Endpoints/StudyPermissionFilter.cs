using Devkit.Server.Application.Workspace;

namespace Devkit.Server.Api.Endpoints;

// Shared read endpoints support module selectors; writes and module operations stay separately authorized.
internal sealed class StudyPermissionFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var c = context.HttpContext;
        var actor = await c.RequestServices.GetRequiredService<IWorkspaceAccess>().CurrentAsync(c.RequestAborted);
        var path = ((RouteEndpoint)c.GetEndpoint()!).RoutePattern.RawText!;
        var read = HttpMethods.IsGet(c.Request.Method);
        if (path.EndsWith("/capabilities") || read && path.EndsWith("/knowledge-bases"))
            actor.Require("exam-study.access");
        else if (path.Contains("/jobs"))
            actor.RequireAny("study.jobs.view", "system.storage.manage");
        else if (path.Contains("/questions") || path.Contains("/generation-jobs"))
            actor.Require("study.questions.manage");
        else if (path.Contains("/attempts") || path.Contains("/mistakes") || path.EndsWith("/progress"))
            actor.Require("study.practice");
        else if (path.EndsWith("/search") || path.EndsWith("/ask") || path.EndsWith("/history"))
            actor.Require("study.search");
        else if (path.Contains("/projects"))
        {
            if (read) actor.RequireAny("study.projects.manage", "study.search", "study.practice");
            else actor.Require("study.projects.manage");
        }
        else if (read) actor.RequireAny("study.knowledge.manage", "study.questions.manage");
        else actor.Require("study.knowledge.manage");
        return await next(context);
    }
}
