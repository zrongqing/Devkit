using Devkit.Server.Domain.Common;

namespace Devkit.Server.Domain.Workspace;

public abstract class OwnedEntity : AuditableEntity
{
    public Guid OwnerId { get; set; }
    public int Revision { get; set; } = 1;
}

public sealed class KnowledgeBase : OwnedEntity
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Tags { get; set; } = "";
}

public sealed class KnowledgeSource : OwnedEntity
{
    public Guid KnowledgeBaseId { get; set; }
    public string Title { get; set; } = "";
    public Guid? FileId { get; set; }
    public string Kind { get; set; } = "manual";
    public string Text { get; set; } = "";
    public string Status { get; set; } = "queued";
    public string Warning { get; set; } = "";
    public int PublishedRevision { get; set; }
}

public sealed class SourceVersion : OwnedEntity
{
    public Guid SourceId { get; set; }
    public Guid? FileId { get; set; }
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public string BlocksJson { get; set; } = "[]";
}

public sealed class KnowledgeChunk : OwnedEntity
{
    public Guid KnowledgeBaseId { get; set; }
    public Guid SourceId { get; set; }
    public Guid VersionId { get; set; }
    public int Ordinal { get; set; }
    public int? Page { get; set; }
    public string Location { get; set; } = "";
    public string Text { get; set; } = "";
    public string Heading { get; set; } = "";
    public string EmbeddingProfile { get; set; } = "";
}

public sealed class StudyProject : OwnedEntity
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime? TargetDate { get; set; }
    public string KnowledgeBaseIdsJson { get; set; } = "[]";
}

public sealed class Question : OwnedEntity
{
    public Guid? GenerationJobId { get; set; }
    public int GenerationOrdinal { get; set; }
    public Guid KnowledgeBaseId { get; set; }
    public string Type { get; set; } = "single";
    public string Stem { get; set; } = "";
    public string OptionsJson { get; set; } = "[]";
    public string AnswersJson { get; set; } = "[]";
    public string Explanation { get; set; } = "";
    public string CitationsJson { get; set; } = "[]";
    public string Tags { get; set; } = "";
    public string Difficulty { get; set; } = "normal";
    public string Status { get; set; } = "draft";
}

public sealed class StudyAttempt : OwnedEntity
{
    public Guid ProjectId { get; set; }
    public string Mode { get; set; } = "exam";
    public string Status { get; set; } = "active";
    public DateTime? DeadlineUtc { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public string QuestionsJson { get; set; } = "[]";
    public string AnswersJson { get; set; } = "{}";
    public decimal? Score { get; set; }
    public int PassScore { get; set; } = 60;
}

public sealed class Mistake : OwnedEntity
{
    public Guid ProjectId { get; set; }
    public Guid QuestionId { get; set; }
    public int WrongCount { get; set; }
    public int AnswerCount { get; set; }
    public bool Mastered { get; set; }
    public DateTime LastAnsweredAtUtc { get; set; }
}

public sealed class QueryHistory : OwnedEntity
{
    public Guid ProjectId { get; set; }
    public string Query { get; set; } = "";
    public string Answer { get; set; } = "";
    public string CitationsJson { get; set; } = "[]";
}

public sealed class WorkJob : OwnedEntity
{
    public string Kind { get; set; } = "ingest";
    public Guid TargetId { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public string Status { get; set; } = "queued";
    public int Progress { get; set; }
    public int Attempts { get; set; }
    public string Error { get; set; } = "";
    public DateTime? LeaseUntilUtc { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTime? RetryAfterUtc { get; set; }
}
