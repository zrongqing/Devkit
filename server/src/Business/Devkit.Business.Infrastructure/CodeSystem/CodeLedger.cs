using System.Data;
using Devkit.Business.Application.CodeSystem;
using Devkit.Business.Application.Workspace;
using Devkit.Business.Domain.CodeSystem;
using Devkit.Business.Infrastructure.Persistence;
using Devkit.Commons.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Devkit.Business.Infrastructure.CodeSystem;

public sealed class CodeLedger(IServiceScopeFactory scopes, TimeProvider clock) : ICodeIssueStore
{
    private static IQueryable<CodeRule> LockRules(BusinessDbContext db) => db.Database.IsSqlServer()
        ? db.Set<CodeRule>().FromSqlRaw("SELECT * FROM sys_code_rule WITH (UPDLOCK, HOLDLOCK)")
        : db.Set<CodeRule>();

    // Isolate the ledger transaction (and each execution-strategy retry) from the caller's unit of work.
    private async Task<T> WriteAsync<T>(Func<BusinessDbContext, Task<T>> operation, CancellationToken ct)
    {
        await using var strategyScope = scopes.CreateAsyncScope();
        var strategy = strategyScope.ServiceProvider.GetRequiredService<BusinessDbContext>().Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async token =>
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<BusinessDbContext>();
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                token.ThrowIfCancellationRequested();
                var result = await operation(db);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return result;
            }, ct);
        }
        catch (RetryLimitExceededException)
        {
            throw new BusinessException(503, "dependency_unavailable", "数据库暂时不可用，请使用原请求重试。");
        }
    }

    public Task<CodeRule> CreateAsync(CodeScenario scenario, string name, CodeDefinition definition, Guid actor, bool seed, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100) throw new BusinessException(400, "invalid_request", "规则名称需为 1 至 100 个字符。");
        definition.Validate();
        return WriteAsync(async db =>
        {
            var existing = await LockRules(db).SingleOrDefaultAsync(x => x.RuleKey == scenario.RuleKey, ct);
            if (existing != null)
            {
                if (seed && existing.ModuleKey == scenario.ModuleKey) return existing;
                throw Conflict("rule_key_exists", "该规则键已存在。");
            }
            var now = clock.GetUtcNow().UtcDateTime;
            var rule = new CodeRule
            {
                RuleKey = scenario.RuleKey,
                ModuleKey = scenario.ModuleKey,
                Name = name.Trim(),
                DraftJson = JsonData.Write(definition),
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                CreatedBy = actor,
                UpdatedBy = actor
            };
            db.Add(rule);
            AddEvent(db, rule, "create", seed ? "启动初始化" : null);
            return rule;
        }, ct);
    }

    public Task<CodeRule> ChangeAsync(Guid id, string[] modules, long revision, string action, CodeDefinition? draft, string? reason, Guid actor, CancellationToken ct)
        => WriteAsync(async db =>
        {
            var rule = await LockRules(db).SingleOrDefaultAsync(x => x.Id == id && modules.Contains(x.ModuleKey), ct) ?? throw Missing();
            if (revision <= 0) throw new BusinessException(400, "invalid_request", "修订号必须为正数。");
            if (revision != rule.Revision) throw Conflict("revision_conflict", "规则已修改，请刷新并比较差异。");
            if (action != "save_draft" && (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 256))
                throw new BusinessException(400, "invalid_request", "变更原因需为 1 至 256 个字符。");
            var now = clock.GetUtcNow().UtcDateTime;
            var active = rule.ActiveVersionNo is int version
                ? JsonData.Read<CodeDefinition>((await db.Set<CodeVersion>().FindAsync([id, version], ct))!.DefinitionJson) : null;
            switch (action)
            {
                case "save_draft":
                    if (draft == null) throw CodeDefinition.Invalid("definition", "请提供草稿。");
                    draft.Validate(active);
                    rule.DraftJson = JsonData.Write(draft);
                    break;
                case "publish" when rule.DraftJson != null:
                    JsonData.Read<CodeDefinition>(rule.DraftJson).Validate(active);
                    var next = (rule.ActiveVersionNo ?? 0) + 1;
                    db.Add(new CodeVersion
                    {
                        RuleId = id,
                        VersionNo = next,
                        DefinitionJson = rule.DraftJson,
                        PublishedBy = actor,
                        PublishedAtUtc = now,
                        Reason = reason!.Trim()
                    });
                    await db.SaveChangesAsync(ct);
                    rule.ActiveVersionNo = next;
                    rule.DraftJson = null;
                    if (rule.Status == "unpublished") rule.Status = "enabled";
                    break;
                case "disable" when rule.Status == "enabled": rule.Status = "disabled"; break;
                case "enable" when rule.Status == "disabled": rule.Status = "enabled"; break;
                default: throw Conflict("invalid_rule_state", "当前状态不允许此操作。");
            }
            rule.UpdatedBy = actor;
            rule.UpdatedAtUtc = now;
            rule.Revision++;
            AddEvent(db, rule, action, reason?.Trim());
            return rule;
        }, ct);

    public Task<CodeIssue> IssueAsync(string ruleKey, string moduleKey, Guid requestId, Guid businessId, Guid actorId, CancellationToken ct)
        => WriteAsync(async db =>
        {
            var rule = await LockRules(db).SingleOrDefaultAsync(x => x.RuleKey == ruleKey && x.ModuleKey == moduleKey, ct) ?? throw Missing();
            var issues = db.Set<CodeIssue>().Where(x => x.RuleId == rule.Id);
            var existing = await issues.SingleOrDefaultAsync(x => x.RequestId == requestId, ct);
            if (existing != null)
                return existing.BusinessId == businessId ? existing : throw Conflict("idempotency_conflict", "请求已用于其他业务对象。");
            if (await issues.AnyAsync(x => x.BusinessId == businessId, ct)) throw Conflict("business_already_numbered", "该对象已经取号，请使用原请求。");
            if (rule.Status == "disabled") throw Conflict("rule_disabled", "规则已停用。");
            if (rule.ActiveVersionNo is not int version) throw Conflict("invalid_rule_state", "规则尚未发布。");
            var snapshot = await db.Set<CodeVersion>().FindAsync([rule.Id, version], ct);
            var definition = JsonData.Read<CodeDefinition>(snapshot!.DefinitionJson);
            var now = db.Database.IsSqlServer()
                ? DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS [Value]").SingleAsync(ct), DateTimeKind.Utc)
                : clock.GetUtcNow().UtcDateTime;
            var period = definition.Render(now, definition.Start).PeriodKey;
            var counter = await db.Set<CodeCounter>().FindAsync([rule.Id, period], ct);
            var value = counter == null ? definition.Start : counter.LastValue + definition.Step;
            if (value > definition.MaxValue) throw Conflict("sequence_exhausted", "本期间流水号已达到当前位宽上限。");
            var code = definition.Render(now, value).Code;
            if (await issues.AnyAsync(x => x.Code == code, ct)) throw Conflict("code_collision", "生成结果与历史编码冲突。");
            if (counter == null)
            {
                counter = new CodeCounter { RuleId = rule.Id, PeriodKey = period };
                db.Add(counter);
            }
            counter.LastValue = value;
            counter.UpdatedAtUtc = now;
            var issue = new CodeIssue
            {
                RuleId = rule.Id,
                VersionNo = version,
                PeriodKey = period,
                SequenceValue = value,
                Code = code,
                RequestId = requestId,
                BusinessId = businessId,
                IssuedBy = actorId,
                IssuedAtUtc = now
            };
            db.Add(issue);
            return issue;
        }, ct);

    private static void AddEvent(BusinessDbContext db, CodeRule rule, string action, string? reason) =>
        db.Add(new CodeEvent
        {
            RuleId = rule.Id,
            Action = action,
            Revision = rule.Revision,
            VersionNo = rule.ActiveVersionNo,
            ActorId = rule.UpdatedBy,
            OccurredAtUtc = rule.UpdatedAtUtc,
            Reason = reason
        });
    internal static BusinessException Missing() => new(404, "rule_not_found", "规则不存在或无权访问。");
    private static BusinessException Conflict(string code, string message) => new(409, code, message);
}
