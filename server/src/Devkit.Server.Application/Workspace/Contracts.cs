using System.Text.Json;
using Devkit.Server.Domain.FileStorage;
using Devkit.Server.Domain.Workspace;

namespace Devkit.Server.Application.Workspace;

public static class JsonData
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public static T Read<T>(string value) => JsonSerializer.Deserialize<T>(value, Options)!;
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
}

public sealed class BusinessException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

public sealed record Actor(Guid Id, bool Administrator, IReadOnlyList<string> Permissions)
{
    public void Require(string permission)
    {
        if (!Administrator && !Permissions.Contains(permission)) throw new BusinessException(403, "forbidden", "没有此功能的权限。");
    }
    public void Own(Guid owner)
    {
        if (!Administrator && Id != owner) throw new BusinessException(404, "not_found", "内容不存在或无权访问。");
    }
}

public static class PermissionCatalog
{
    public static readonly string[] All = ["exam-study.access", "system.files.manage", "system.storage.manage", "system.identity.manage"];
}

public sealed record TextBlock(string Text, string Heading, string Location, int? Page);
public sealed record ExtractedDocument(IReadOnlyList<TextBlock> Blocks, string Warning);
public sealed record Citation(Guid ChunkId, Guid SourceId, Guid VersionId, Guid KnowledgeBaseId, Guid? FileId, string Title, string Location, int? Page, string Text);
public sealed record SearchHit(Citation Citation, double Score);
public sealed record SearchRequest(string Query, string Mode = "keyword", Guid? KnowledgeBaseId = null, Guid? SourceId = null, int Limit = 10, int Offset = 0);
public sealed record SearchResponse(IReadOnlyList<SearchHit> Items, string Mode, string? Warning = null);
public sealed record NamedRequest(string Name, string Description = "", string Tags = "", int? Revision = null);
public sealed record SourceRequest(string Title, Guid? FileId, string Text = "", int? Revision = null);
public sealed record ProjectRequest(string Name, string Description, DateTime? TargetDate, Guid[] KnowledgeBaseIds, int? Revision = null);
public sealed record QuestionOption(string Id, string Text);
public sealed record QuestionRequest(Guid KnowledgeBaseId, string Type, string Stem, QuestionOption[] Options, string[] Answers, string Explanation, Citation[] Citations, string Tags = "", string Difficulty = "normal", int? Revision = null);
public sealed record GenerationRequest(Guid KnowledgeBaseId, Guid? SourceId, int Count = 10, string Type = "single", string Difficulty = "normal", string Tags = "");
public sealed record AttemptRequest(string Mode = "exam", int Count = 20, int Minutes = 30, int PassScore = 60, string Selection = "random", string[]? Types = null);
public sealed record AnswerRequest(string[] Answers, int Revision);
public sealed record QuestionSnapshot(Guid Id, int Revision, string Type, string Stem, QuestionOption[] Options, string[] Answers, string Explanation, Citation[] Citations);
public sealed record AttemptQuestion(Guid Id, string Type, string Stem, QuestionOption[] Options, string[] Selected, bool Answered, string[]? Answers, string? Explanation, Citation[]? Citations, bool? Correct);
public sealed record AttemptView(Guid Id, Guid ProjectId, string Mode, string Status, int Revision, DateTime? DeadlineUtc, DateTime ServerTimeUtc, decimal? Score, int PassScore, IReadOnlyList<AttemptQuestion> Questions);
public sealed record FileView(Guid Id, string Name, string ContentType, long Length, string Sha256, string Purpose, Guid OwnerId, string Status, int ReferenceCount, DateTime CreatedAtUtc);
public sealed record OpenedFile(Stream Stream, string Name, string ContentType);
public sealed record StorageView(Guid Id, string Name, string RootPath, bool Writable, long? AvailableBytes, long FileCount, long TotalBytes);
public sealed record LocationRequest(string Name, string RootPath);
public sealed record MigrationRequest(Guid SourceLocationId, Guid TargetLocationId);
public sealed record CapabilityView(bool ChatConfigured, bool EmbeddingConfigured, bool QdrantAvailable);
public sealed record SourceDetailView(KnowledgeSource Source, IReadOnlyList<SourceVersion> Versions, IReadOnlyList<KnowledgeChunk> Chunks);
public sealed record AttemptSummary(Guid Id, string Mode, string Status, decimal? Score, int PassScore, DateTime? DeadlineUtc, DateTime CreatedAtUtc);
public sealed record ProgressView(IReadOnlyList<AttemptSummary> Attempts, IReadOnlyList<Mistake> Mistakes, int Answered, int Correct);
public sealed record SuccessView(bool Ok = true);
public sealed record JobCreatedView(Guid Id);

public interface IWorkspaceAccess { Task<Actor> CurrentAsync(CancellationToken ct = default); }
public interface IFileService
{
    Task<FileView> UploadAsync(Actor actor, string name, string purpose, Stream stream, CancellationToken ct);
    Task<IReadOnlyList<FileView>> ListAsync(Actor actor, CancellationToken ct);
    Task<OpenedFile> OpenAsync(Actor actor, Guid id, CancellationToken ct);
    Task<OpenedFile> OpenInternalAsync(Guid id, CancellationToken ct);
    Task LinkAsync(Actor actor, Guid id, Guid entityId, Guid referenceOwnerId, string module, CancellationToken ct);
    Task<IReadOnlyList<FileReference>> ReferencesAsync(Actor actor, Guid id, CancellationToken ct);
    Task UnlinkAsync(Guid entityId, CancellationToken ct);
    Task PurgeAsync(Actor actor, Guid id, CancellationToken ct);
    Task<IReadOnlyList<StorageView>> LocationsAsync(Actor actor, CancellationToken ct);
    Task<StorageLocation> AddLocationAsync(Actor actor, LocationRequest request, CancellationToken ct);
    Task<Guid> MigrateAsync(Actor actor, MigrationRequest request, CancellationToken ct);
    Task CleanupAsync(Actor actor, Guid jobId, CancellationToken ct);
}
public interface IDocumentProcessor
{
    Task<ExtractedDocument> ExtractAsync(Guid fileId, CancellationToken ct);
    IReadOnlyList<TextBlock> Split(IReadOnlyList<TextBlock> blocks);
}
public interface IKnowledgeIndex
{
    Task<bool> AvailableAsync(CancellationToken ct);
    Task IndexAsync(IReadOnlyList<Domain.Workspace.KnowledgeChunk> chunks, CancellationToken ct);
    Task<IReadOnlyList<(Guid Id, double Score)>> SearchAsync(string query, string mode, Guid ownerId, IReadOnlyList<Guid> knowledgeBaseIds, IReadOnlyList<Guid> versionIds, Guid? sourceId, int limit, CancellationToken ct);
    Task DeleteAsync(Guid sourceId, CancellationToken ct);
}
public interface IModelGateway
{
    bool ChatConfigured { get; }
    bool EmbeddingConfigured { get; }
    string EmbeddingProfile { get; }
    Task<float[][]> EmbedAsync(IReadOnlyList<string> input, CancellationToken ct);
    Task<string> ChatAsync(string instruction, string content, CancellationToken ct);
}
