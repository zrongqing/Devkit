namespace Devkit.Modules.Ssamc.Core.ApiCodeCollector;

internal sealed class SourceFileAnalysis
{
    public List<ApiSourceInfo> ApiSources { get; init; } = [];

    public Dictionary<string, List<string>> ExecutionSources { get; init; } =
        new(StringComparer.Ordinal);
}
