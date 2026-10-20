namespace Devkit.FileStorage.Infrastructure.Files;

public sealed class FileStorageOptions
{
    public string DataRoot { get; set; } = OperatingSystem.IsWindows() ? @"D:\.server data\devkit" : "/var/lib/devkit";
    public long MaximumUploadBytes { get; set; } = 20 * 1024 * 1024;
    public long MinimumFreeBytes { get; set; } = 100 * 1024 * 1024;
    public Dictionary<string, FileClientOptions> Clients { get; set; } = [];
    public Dictionary<string, UploadPurposeOptions> Purposes { get; set; } = new(StringComparer.Ordinal)
    {
        ["general"] = new() { UploadPermissions = ["system.files.manage"], DownloadPermissions = ["system.files.manage"] },
        ["exam-study"] = new()
        {
            UploadPermissions = ["study.knowledge.manage", "exam-study.access"],
            DownloadPermissions = ["exam-study.access", "study.projects.manage", "study.knowledge.manage", "study.search", "study.practice", "study.questions.manage", "study.jobs.view"],
            Extensions = [".pdf", ".docx"], ValidateDocumentSignature = true
        }
    };
}
public sealed class FileClientOptions
{
    public string ApiKey { get; set; } = "";
    public string Module { get; set; } = "";
}
public sealed class UploadPurposeOptions
{
    public string[] UploadPermissions { get; set; } = [];
    public string[] DownloadPermissions { get; set; } = [];
    public string[] Extensions { get; set; } = [];
    public bool ValidateDocumentSignature { get; set; }
}
