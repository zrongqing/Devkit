using Devkit.Business.Domain.Abstractions;
using Devkit.Business.Domain.Workspace;
using Devkit.Commons.Serialization;
using Devkit.DomainCommons.Querying;

namespace Devkit.Business.Application.Workspace;

public sealed class StudyPageService(IWorkspaceStore store, StudyService study)
{
    private static readonly Dictionary<string, string> Labels = new()
    {
        ["queued"]="等待处理", ["running"]="处理中", ["completed"]="已完成", ["waiting"]="待配置", ["failed"]="失败",
        ["ready"]="可检索", ["draft"]="待审核", ["approved"]="已审核", ["stale"]="待复核", ["disabled"]="已停用",
        ["ingest"]="提取与关键词索引", ["dense"]="语义增强", ["generate"]="AI 出题", ["remove-index"]="移除索引",
        ["single"]="单选", ["multiple"]="多选", ["boolean"]="判断", ["practice"]="练习", ["exam"]="模拟考试", ["active"]="进行中", ["submitted"]="已交卷",
    };
    private static object Label(string value) => value + " " + Labels.GetValueOrDefault(value, value);
    private static PageQuery DefaultOrder(PageQuery query) => (query.Sorts?.Length ?? 0) == 0 ? query with { Sorts = [new("createdAtUtc", "desc")] } : query;
    public Task<PageResult<KnowledgeBase>> BasesAsync(Actor actor, PageQuery query, bool all, CancellationToken ct)
    { actor.Require("exam-study.access"); return store.PageAsync<KnowledgeBase>(DefaultOrder(query), x => actor.Administrator && all || x.OwnerId == actor.Id, ct); }
    public async Task<PageResult<StudyProject>> ProjectsAsync(Actor actor, PageQuery query, bool all, CancellationToken ct)
    {
        PageQueries.Validate(query); actor.Require("exam-study.access");
        if (!(query.Filters ?? []).Any(x => x.Field == "knowledgeBases") && !(query.Sorts ?? []).Any(x => x.Field == "knowledgeBases"))
            return await store.PageAsync<StudyProject>(DefaultOrder(query), x => actor.Administrator && all || x.OwnerId == actor.Id, ct);
        var bases = await study.BasesAsync(actor, all, ct);
        return PageQueries.Snapshot(await study.ProjectsAsync(actor, all, ct), DefaultOrder(query), new Dictionary<string, Func<StudyProject, object?>>
        { ["knowledgeBases"] = row => bases.Where(b => JsonData.Read<Guid[]>(row.KnowledgeBaseIdsJson).Contains(b.Id)).Select(b => b.Name).ToArray() });
    }
    public async Task<PageResult<KnowledgeSource>> SourcesAsync(Actor actor, Guid id, PageQuery query, CancellationToken ct)
    {
        PageQueries.Validate(query); await study.GetAsync<KnowledgeBase>(actor, id, ct);
        if (!(query.Filters ?? []).Any(f => f.Field == "status")) return await store.PageAsync<KnowledgeSource>(DefaultOrder(query), x => x.KnowledgeBaseId == id, ct);
        return PageQueries.Snapshot(await study.SourcesAsync(actor, id, ct), DefaultOrder(query), new Dictionary<string, Func<KnowledgeSource, object?>> { ["status"] = x => Label(x.Status) });
    }
    public async Task<PageResult<Question>> QuestionsAsync(Actor actor, Guid? id, PageQuery query, bool all, CancellationToken ct)
    {
        PageQueries.Validate(query); actor.Require("exam-study.access");
        if (id.HasValue) await study.GetAsync<KnowledgeBase>(actor, id.Value, ct);
        if (!(query.Filters ?? []).Any(f => f.Field is "status" or "type"))
            return await store.PageAsync<Question>(DefaultOrder(query), x => (actor.Administrator && all || x.OwnerId == actor.Id) && (!id.HasValue || x.KnowledgeBaseId == id), ct);
        return PageQueries.Snapshot(await study.QuestionsAsync(actor, id, all, ct), DefaultOrder(query), new Dictionary<string, Func<Question, object?>> { ["status"] = x => Label(x.Status), ["type"] = x => Label(x.Type) });
    }
    public async Task<PageResult<WorkJob>> JobsAsync(Actor actor, PageQuery query, CancellationToken ct)
    {
        PageQueries.Validate(query); actor.Require("study.jobs.view");
        if (!(query.Filters ?? []).Any(f => f.Field is "status" or "kind"))
            return await store.PageAsync<WorkJob>(DefaultOrder(query), x => (actor.Administrator || x.OwnerId == actor.Id) && x.Kind != "migrate" && x.Kind != "cleanup", ct);
        var rows = await store.ListAsync<WorkJob>(x => (actor.Administrator || x.OwnerId == actor.Id) && x.Kind != "migrate" && x.Kind != "cleanup", ct);
        return PageQueries.Snapshot(rows, DefaultOrder(query), new Dictionary<string, Func<WorkJob, object?>> { ["status"] = x => Label(x.Status), ["kind"] = x => Label(x.Kind) });
    }
    public async Task<PageResult<Mistake>> MistakesAsync(Actor actor, Guid id, PageQuery query, CancellationToken ct)
    {
        PageQueries.Validate(query); actor.Require("study.practice"); var progress = await study.ProgressAsync(actor, id, ct);
        return PageQueries.Snapshot(progress.Mistakes, query, new Dictionary<string, Func<Mistake, object?>>
        { ["question"] = x => progress.QuestionNames.GetValueOrDefault(x.QuestionId, "题目已归档，可在历史试卷查看"), ["mastered"] = x => x.Mastered ? "true 已掌握" : "false 未掌握" });
    }
    public async Task<PageResult<AttemptSummary>> AttemptsAsync(Actor actor, Guid id, PageQuery query, CancellationToken ct)
    {
        PageQueries.Validate(query); actor.Require("study.practice"); var progress = await study.ProgressAsync(actor, id, ct);
        return PageQueries.Snapshot(progress.Attempts, DefaultOrder(query), new Dictionary<string, Func<AttemptSummary, object?>> { ["status"] = x => Label(x.Status), ["mode"] = x => Label(x.Mode) });
    }
}
