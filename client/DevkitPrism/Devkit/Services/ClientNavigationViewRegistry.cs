namespace Devkit.Services;

public static class ClientNavigationViewRegistry
{
    private const string UnavailableViewName = "UnavailableView";

    private static readonly IReadOnlyDictionary<string, string> ViewNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["home"] = "HomeView",
            ["system-status"] = "SystemStatusView",
            ["settings"] = "SettingView",
            ["about"] = "AboutView"
        };

    public static string? Resolve(string? viewKey)
    {
        if (string.IsNullOrWhiteSpace(viewKey))
        {
            return null;
        }

        return ViewNames.TryGetValue(viewKey, out var viewName)
            ? viewName
            : UnavailableViewName;
    }
}
