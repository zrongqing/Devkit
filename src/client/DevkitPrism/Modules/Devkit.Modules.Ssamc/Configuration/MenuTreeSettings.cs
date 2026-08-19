namespace Ssamc.Configuration;

public sealed class MenuTreeSettings
{
    public string PageEnvironmentKey { get; set; } =
        SsamcEnvironment.DevelopmentEnvironment;

    public Dictionary<string, string> DatabaseConnections { get; set; } = [];

    public string? GetDatabaseConnection(string environmentKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentKey);

        return DatabaseConnections?
            .FirstOrDefault(pair => pair.Key.Equals(
                environmentKey,
                StringComparison.OrdinalIgnoreCase))
            .Value;
    }
}
