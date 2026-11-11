using System.Globalization;
using System.Text.RegularExpressions;
using Devkit.Business.Api.Contracts;
using Devkit.Business.Application.CodeSystem;
using Devkit.Business.Application.Workspace;
using Devkit.Business.Domain.CodeSystem;
using Devkit.Business.Infrastructure.CodeSystem;
using Devkit.DomainCommons.Querying;

namespace Devkit.Business.Api.Endpoints;

public sealed class CodeSystemEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/code-system").RequireAuthorization().WithTags("编码规则")
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409).ProducesProblem(503);
        group.MapPost("/scenarios/query", (HttpContext c, CodeManagement s, PageQuery q) => Wrap(c, s.Scenarios(q)));
        group.MapPost("/rules/query", async (HttpContext c, CodeManagement s, PageQuery q, CancellationToken ct) => Wrap(c, await s.RulePage(q, ct)));
        group.MapPost("/preview", (HttpContext c, CodeManagement s, PreviewCode r) =>
        {
            if (r.SampleAt == null || !Regex.IsMatch(r.SampleAt, @"\A\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?(Z|[+-]\d{2}:\d{2})\z") ||
                !DateTimeOffset.TryParse(r.SampleAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var at))
                throw new BusinessException(400, "invalid_request", "sampleAt 需要带时区的日期时间。");
            return Wrap(c, s.Preview(r.RuleKey, Required(r.Definition), at, r.SampleValue));
        });
        group.MapPost("/rules", async (HttpContext c, CodeManagement s, CreateCodeRule r, CancellationToken ct) =>
        {
            var rule = await s.Create(r.RuleKey, r.Name, Required(r.Definition), ct);
            return TypedResults.Created($"/api/v1/code-system/rules/{rule.Id}", Wrap(c, Summary(rule)));
        });
        group.MapGet("/rules/{id:guid}", async (HttpContext c, CodeManagement s, Guid id, CancellationToken ct) => Wrap(c, await s.Detail(id, ct)));
        group.MapPut("/rules/{id:guid}/draft", async (HttpContext c, CodeManagement s, Guid id, SaveCodeDraft r, CancellationToken ct) =>
            Wrap(c, Summary(await s.Change(id, r.ExpectedRevision, "save_draft", Required(r.Definition), null, ct))));
        foreach (var action in new[] { "publish", "disable", "enable" })
            group.MapPost($"/rules/{{id:guid}}/{action}", async (HttpContext c, CodeManagement s, Guid id, CodeTransition r, CancellationToken ct) =>
                Wrap(c, Summary(await s.Change(id, r.ExpectedRevision, action, null, r.Reason, ct))));
        group.MapGet("/rules/{id:guid}/versions/{versionNo:int}", async (HttpContext c, CodeManagement s, Guid id, int versionNo, CancellationToken ct) =>
            Wrap(c, await s.Version(id, versionNo, ct)));
        group.MapPost("/rules/{id:guid}/versions/query", async (HttpContext c, CodeManagement s, Guid id, PageQuery q, CancellationToken ct) =>
            Wrap(c, await s.Versions(id, q, ct)));
        group.MapPost("/rules/{id:guid}/events/query", async (HttpContext c, CodeManagement s, Guid id, PageQuery q, CancellationToken ct) =>
            Wrap(c, await s.Events(id, q, ct)));
        group.MapPost("/rules/{id:guid}/issues/query", async (HttpContext c, CodeManagement s, Guid id, PageQuery q, CancellationToken ct) =>
            Wrap(c, await s.Issues(id, q, ct)));
    }
    // Minimal API binding can reject the body before the endpoint delegate runs.
    public static void UseBindingProblems(WebApplication app)
    {
        app.UseWhen(c => c.Request.Path.StartsWithSegments("/api/v1/code-system"), branch =>
            branch.Use(async (context, next) =>
            {
                try { await next(); }
                catch (BadHttpRequestException e) when (e.StatusCode == 400 && !context.Response.HasStarted)
                {
                    context.Response.StatusCode = 400;
                }
                if (context.Response.StatusCode == 400 && !context.Response.HasStarted && string.IsNullOrEmpty(context.Response.ContentType))
                    await Results.Problem(statusCode: 400, title: "请求体缺失、字段类型错误或包含未知参数。",
                        extensions: new Dictionary<string, object?> { ["code"] = "invalid_request", ["traceId"] = context.TraceIdentifier }).ExecuteAsync(context);
            }));
    }

    private static CodeDefinition Required(CodeDefinition? value) => value ?? throw CodeDefinition.Invalid("definition", "请提供规则定义。");
    private static CodeChangeResult Summary(CodeRule r) => new(r.Id, r.Revision, r.Status, r.ActiveVersionNo);
    private static ApiResponse<T> Wrap<T>(HttpContext c, T value) => new(value, c.TraceIdentifier);
}
