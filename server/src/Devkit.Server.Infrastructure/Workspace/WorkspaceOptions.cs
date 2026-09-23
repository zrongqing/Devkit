namespace Devkit.Server.Infrastructure.Workspace;

public sealed class WorkspaceOptions
{
    public string DataRoot { get; set; } = OperatingSystem.IsWindows() ? @"D:\.server data\devkit" : "/var/lib/devkit";
    public long MaximumUploadBytes { get; set; } = 20 * 1024 * 1024;
    public long MinimumFreeBytes { get; set; } = 100 * 1024 * 1024;
    public string QdrantUrl { get; set; } = "http://localhost:6333";
    public string QdrantApiKey { get; set; } = "";
    public ModelOptions Chat { get; set; } = new();
    public ModelOptions Embedding { get; set; } = new();
}
public sealed class ModelOptions
{
    public string BaseUrl { get; set; } = "";
    public string Model { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 90;
    public bool Configured => Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && !string.IsNullOrWhiteSpace(Model);
}
