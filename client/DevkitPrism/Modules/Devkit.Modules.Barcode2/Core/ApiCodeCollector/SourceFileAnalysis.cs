namespace Barcode2.Core.ApiCodeCollector;

internal sealed class SourceFileAnalysis
{
    public List<ApiSourceInfo> ApiSources { get; init; } = [];

    public Dictionary<string, List<string>> ExecutionSources { get; init; } =
        new(StringComparer.Ordinal);

    public Dictionary<string, List<string>> ExecutionSourcesByName { get; init; } =
        new(StringComparer.Ordinal);
}
