using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Devkit.Business.Application.Workspace;
using Devkit.Business.Domain.CodeSystem;

namespace Devkit.Business.Application.CodeSystem;

public sealed record CodeScenario(string RuleKey, string ModuleKey, string ObjectType);
public sealed class CodeSettings
{
    public CodeScenario[] Scenarios { get; set; } = [];
    public string[] RoleKeys { get; set; } = [];
    public string BootstrapKey { get; set; } = "";
    public SeedDraft[] Drafts { get; set; } = [];
    public bool Valid() => Scenarios.All(x => x != null && IsKey(x.RuleKey) && IsKey(x.ModuleKey) && !string.IsNullOrWhiteSpace(x.ObjectType))
        && Scenarios.DistinctBy(x => x.RuleKey).Count() == Scenarios.Length
        && (RoleKeys.Length == 0 || BootstrapKey.Length >= 32);
    private static bool IsKey(string? value) => value != null && Regex.IsMatch(value, @"\A[a-z][a-z0-9._-]{0,63}\z");
}
public sealed record SeedDraft(string RuleKey, string Name, CodeDefinition Definition);
public sealed record RuleRow(Guid Id, string RuleKey, string ModuleKey, string Name, string Status, int? ActiveVersionNo, DateTime UpdatedAtUtc)
{
    public RuleRow() : this(default!, default!, default!, default!, default!, default!, default!) { }
}
public sealed record RuleDetail(RuleRow Rule, long Revision, CodeDefinition? Active, CodeDefinition? Draft);
public sealed record VersionRow(int VersionNo, Guid PublishedBy, DateTime PublishedAtUtc)
{
    public VersionRow() : this(default!, default!, default!) { }
}
public sealed record VersionDetail(VersionRow Version, CodeDefinition Definition, string Reason);
public sealed record EventRow(Guid Id, string Action, long Revision, int? VersionNo, Guid ActorId, DateTime OccurredAtUtc, string? Reason)
{
    public EventRow() : this(default!, default!, default!, default!, default!, default!, null) { }
}
public sealed record IssueRow(Guid IssueId, Guid BusinessId, string Code, int VersionNo, string PeriodKey, long SequenceValue, Guid IssuedBy, DateTime IssuedAtUtc)
{
    public IssueRow() : this(default!, default!, default!, default!, default!, default!, default!, default!) { }
}

public sealed record IssuedCode(Guid IssueId, string RuleKey, Guid BusinessId, string Code, int VersionNo,
    string PeriodKey, long SequenceValue, Guid IssuedBy, DateTime IssuedAtUtc);

public interface ICodeGenerator
{
    Task<IssuedCode> IssueAsync(string ruleKey, Guid requestId, Guid businessId, Guid actorId, CancellationToken ct = default);
}

public sealed class CodeGenerator(CodeSettings settings, ICodeIssueStore ledger) : ICodeGenerator
{
    public async Task<IssuedCode> IssueAsync(string ruleKey, Guid requestId, Guid businessId, Guid actorId, CancellationToken ct = default)
    {
        if (actorId == Guid.Empty || requestId == Guid.Empty || businessId == Guid.Empty)
            throw new BusinessException(400, "invalid_request", "取号需要有效的经办人、请求及业务对象 ID。");
        var scenario = settings.Scenarios.SingleOrDefault(x => x.RuleKey == ruleKey)
            ?? throw new BusinessException(404, "scenario_not_found", "未登记该场景。");
        var issue = await ledger.IssueAsync(ruleKey, scenario.ModuleKey, requestId, businessId, actorId, ct);
        return new(issue.Id, ruleKey, issue.BusinessId, issue.Code, issue.VersionNo, issue.PeriodKey, issue.SequenceValue, issue.IssuedBy, issue.IssuedAtUtc);
    }
}
