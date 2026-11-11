namespace Devkit.Business.Domain.CodeSystem;

public sealed class CodeRule
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string RuleKey { get; set; }
    public required string ModuleKey { get; set; }
    public required string Name { get; set; }
    public string Status { get; set; } = "unpublished";
    public int? ActiveVersionNo { get; set; }
    public string? DraftJson { get; set; }
    public long Revision { get; set; } = 1;
    public Guid CreatedBy { get; set; }
    public Guid UpdatedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class CodeVersion
{
    public Guid RuleId { get; set; }
    public int VersionNo { get; set; }
    public required string DefinitionJson { get; set; }
    public required string Reason { get; set; }
    public Guid PublishedBy { get; set; }
    public DateTime PublishedAtUtc { get; set; }
}

public sealed class CodeCounter
{
    public Guid RuleId { get; set; }
    public required string PeriodKey { get; set; }
    public long LastValue { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class CodeIssue
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid RuleId { get; set; }
    public int VersionNo { get; set; }
    public required string PeriodKey { get; set; }
    public long SequenceValue { get; set; }
    public required string Code { get; set; }
    public Guid RequestId { get; set; }
    public Guid BusinessId { get; set; }
    public Guid IssuedBy { get; set; }
    public DateTime IssuedAtUtc { get; set; }
}

public sealed class CodeEvent
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid RuleId { get; set; }
    public required string Action { get; set; }
    public long Revision { get; set; }
    public int? VersionNo { get; set; }
    public Guid ActorId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string? Reason { get; set; }
}

// The caller authorizes the business object before every call, including replays.
public interface ICodeIssueStore
{
    Task<CodeIssue> IssueAsync(string ruleKey, string moduleKey, Guid requestId, Guid businessId, Guid actorId, CancellationToken ct);
}
