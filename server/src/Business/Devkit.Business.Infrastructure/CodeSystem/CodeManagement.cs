using Devkit.Business.Application.Abstractions;
using Devkit.Business.Application.CodeSystem;
using Devkit.Business.Application.Workspace;
using Devkit.Business.Domain.CodeSystem;
using Devkit.Business.Infrastructure.Persistence;
using Devkit.Commons.Serialization;
using Devkit.DomainCommons.Authorization;
using Devkit.DomainCommons.Querying;
using Devkit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Devkit.Business.Infrastructure.CodeSystem;

public sealed class CodeManagement(BusinessDbContext db, CodeAccess access, CodeSettings settings, CodeLedger ledger)
{

    public PageResult<CodeScenario> Scenarios(PageQuery query)
    {
        var modules = access.Modules("view");
        var q = ValidPage<CodeScenario>(query, "ruleKey");
        // Snapshot sorting is stable. Preorder by the immutable key to break user-sort ties.
        return PageQueries.Snapshot(settings.Scenarios.Where(x => modules.Contains(x.ModuleKey)).OrderBy(x => x.RuleKey, StringComparer.Ordinal), q);
    }

    public CodePreview Preview(string key, CodeDefinition definition, DateTimeOffset at, long value)
    {
        access.Scenario(key, "view");
        definition.Validate();
        return definition.Render(at, value);
    }

    public Task<CodeRule> Create(string key, string name, CodeDefinition definition, CancellationToken ct)
        => ledger.CreateAsync(access.Scenario(key, "edit"), name, definition, access.User.Id, false, ct);

    public Task<CodeRule> Change(Guid id, long revision, string action, CodeDefinition? draft, string? reason, CancellationToken ct)
        => ledger.ChangeAsync(id, access.Modules(action == "save_draft" ? "edit" : "publish"), revision, action, draft, reason, access.User.Id, ct);

    private IQueryable<CodeRule> Rules(string action)
    {
        var modules = access.Modules(action);
        return db.Set<CodeRule>().AsNoTracking().Where(r => modules.Contains(r.ModuleKey));
    }

    private async Task<CodeRule> Rule(Guid id, string action, CancellationToken ct) =>
        await Rules(action).SingleOrDefaultAsync(r => r.Id == id, ct) ?? throw CodeLedger.Missing();

    public async Task<RuleDetail> Detail(Guid id, CancellationToken ct)
    {
        var r = await Rule(id, "view", ct);
        var active = r.ActiveVersionNo is int no ? (await Version(id, no, ct)).Definition : null;
        return new(new(r.Id, r.RuleKey, r.ModuleKey, r.Name, r.Status, r.ActiveVersionNo, r.UpdatedAtUtc), r.Revision, active,
            r.DraftJson == null ? null : JsonData.Read<CodeDefinition>(r.DraftJson));
    }

    public async Task<VersionDetail> Version(Guid id, int no, CancellationToken ct)
    {
        await Rule(id, "view", ct);
        var v = await db.Set<CodeVersion>().AsNoTracking().SingleOrDefaultAsync(x => x.RuleId == id && x.VersionNo == no, ct)
            ?? throw new BusinessException(404, "version_not_found", "版本不存在。");
        return new(new(v.VersionNo, v.PublishedBy, v.PublishedAtUtc), JsonData.Read<CodeDefinition>(v.DefinitionJson), v.Reason);
    }

    public Task<PageResult<RuleRow>> RulePage(PageQuery q, CancellationToken ct)
    {
        var authorizedRules = Rules("view");
        q = PageQueries.WithOptions(q, "status", new Dictionary<string, string> { ["enabled"] = "启用", ["disabled"] = "停用", ["unpublished"] = "未发布" });
        return Page(authorizedRules.Select(r => new RuleRow
        {
            Id = r.Id,
            RuleKey = r.RuleKey,
            ModuleKey = r.ModuleKey,
            Name = r.Name,
            Status = r.Status,
            ActiveVersionNo = r.ActiveVersionNo,
            UpdatedAtUtc = r.UpdatedAtUtc
        }), q, "updatedAtUtc", "id", ct);
    }
    public async Task<PageResult<VersionRow>> Versions(Guid id, PageQuery q, CancellationToken ct)
    {
        await Rule(id, "view", ct);
        return await Page(db.Set<CodeVersion>().Where(x => x.RuleId == id).Select(x => new VersionRow
        {
            VersionNo = x.VersionNo,
            PublishedBy = x.PublishedBy,
            PublishedAtUtc = x.PublishedAtUtc
        }), q, "versionNo", "versionNo", ct);
    }
    public async Task<PageResult<IssueRow>> Issues(Guid id, PageQuery q, CancellationToken ct)
    {
        await Rule(id, "audit", ct);
        return await Page(db.Set<CodeIssue>().Where(x => x.RuleId == id).Select(x => new IssueRow
        {
            IssueId = x.Id,
            BusinessId = x.BusinessId,
            Code = x.Code,
            VersionNo = x.VersionNo,
            PeriodKey = x.PeriodKey,
            SequenceValue = x.SequenceValue,
            IssuedBy = x.IssuedBy,
            IssuedAtUtc = x.IssuedAtUtc
        }), q, "issuedAtUtc", "issueId", ct);
    }
    public async Task<PageResult<EventRow>> Events(Guid id, PageQuery q, CancellationToken ct)
    {
        await Rule(id, "audit", ct);
        q = PageQueries.WithOptions(q, "action", new Dictionary<string, string> { ["create"] = "创建", ["save_draft"] = "保存草稿", ["publish"] = "发布", ["enable"] = "恢复", ["disable"] = "停用" });
        return await Page(db.Set<CodeEvent>().Where(x => x.RuleId == id).Select(x => new EventRow
        {
            Id = x.Id,
            Action = x.Action,
            Revision = x.Revision,
            VersionNo = x.VersionNo,
            ActorId = x.ActorId,
            OccurredAtUtc = x.OccurredAtUtc,
            Reason = x.Reason
        }), q, "occurredAtUtc", "id", ct);
    }

    private static PageQuery ValidPage<T>(PageQuery q, string defaultSort, string direction = "asc")
    {
        PageQueries.Validate(q);
        var fields = typeof(T).GetProperties().Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if ((q.Filters ?? []).Select(f => f.Field).Concat((q.Sorts ?? []).Select(s => s.Field)).Any(name => !fields.Contains(name)))
            throw new QueryValidationException("查询包含未登记的字段。");
        return q.Sorts is { Length: > 0 } ? q : q with { Sorts = [new(defaultSort, direction)] };
    }
    private static Task<PageResult<T>> Page<T>(IQueryable<T> rows, PageQuery q, string sort, string key, CancellationToken ct) =>
        PagedDbContext.QueryPageAsync(rows, ValidPage<T>(q, sort, "desc"), ct, [key]);
}
