using Devkit.Server.Application.Contracts.Navigation;
using Devkit.Server.Infrastructure.Configuration;

namespace Devkit.Server.Infrastructure.Navigation;

internal static class NavigationMenuComposer
{
    public static IReadOnlyList<NavigationMenuItem> Compose(
        NavigationOptions options,
        NavigationAudience audience)
    {
        ArgumentNullException.ThrowIfNull(options);

        var audienceItems = audience == NavigationAudience.Web ? options.Web : options.Client;
        EnsureUniqueIds(options.Common, "Navigation:Common");
        EnsureUniqueIds(audienceItems, $"Navigation:{audience}");

        var merged = new Dictionary<string, NavigationMenuOption>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in options.Common)
        {
            merged[item.Id] = item;
        }

        foreach (var item in audienceItems)
        {
            merged[item.Id] = item;
        }

        ValidateReferencesAndCycles(merged, audience);

        var hiddenIds = merged.Values
            .Where(item => !item.IsVisible)
            .Select(item => item.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var item in merged.Values)
            {
                if (item.ParentId is not null
                    && hiddenIds.Contains(item.ParentId)
                    && hiddenIds.Add(item.Id))
                {
                    changed = true;
                }
            }
        }

        var visible = merged.Values
            .Where(item => !hiddenIds.Contains(item.Id))
            .ToArray();
        var parentIds = visible
            .Where(item => item.ParentId is not null)
            .Select(item => item.ParentId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var item in visible)
        {
            if (string.IsNullOrWhiteSpace(item.Title))
            {
                throw new InvalidOperationException($"Navigation menu '{item.Id}' for {audience} requires a title.");
            }

            var isDirectory = parentIds.Contains(item.Id);
            if (isDirectory && !string.IsNullOrWhiteSpace(item.TargetKey))
            {
                throw new InvalidOperationException($"Navigation directory '{item.Id}' for {audience} cannot define TargetKey.");
            }

            if (!isDirectory && string.IsNullOrWhiteSpace(item.TargetKey))
            {
                throw new InvalidOperationException($"Navigation page '{item.Id}' for {audience} requires TargetKey.");
            }
        }

        var home = visible.SingleOrDefault(item => string.Equals(item.Id, "home", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Navigation configuration for {audience} requires a visible 'home' menu.");
        if (home.ParentId is not null
            || !string.Equals(home.TargetKey, "home", StringComparison.OrdinalIgnoreCase)
            || home.IsClosable)
        {
            throw new InvalidOperationException($"Navigation 'home' for {audience} must be a root page with TargetKey 'home' and IsClosable false.");
        }

        return visible
            .OrderBy(item => item.Order)
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Select(item => new NavigationMenuItem(
                item.Id,
                item.ParentId,
                item.Title,
                item.TargetKey,
                item.IconKey,
                item.Order,
                item.IsClosable,
                item.RequiredPermission))
            .ToArray();
    }

    public static bool TryValidateAll(NavigationOptions options)
    {
        try
        {
            Compose(options, NavigationAudience.Web);
            Compose(options, NavigationAudience.Client);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void EnsureUniqueIds(IReadOnlyCollection<NavigationMenuOption> items, string section)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
            {
                throw new InvalidOperationException($"{section} contains a menu without an Id.");
            }

            if (!ids.Add(item.Id))
            {
                throw new InvalidOperationException($"{section} contains duplicate menu Id '{item.Id}'.");
            }
        }
    }

    private static void ValidateReferencesAndCycles(
        IReadOnlyDictionary<string, NavigationMenuOption> items,
        NavigationAudience audience)
    {
        foreach (var item in items.Values)
        {
            if (item.ParentId is not null && !items.ContainsKey(item.ParentId))
            {
                throw new InvalidOperationException($"Navigation menu '{item.Id}' for {audience} references missing parent '{item.ParentId}'.");
            }

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var current = item;
            while (current.ParentId is not null)
            {
                if (!visited.Add(current.Id))
                {
                    throw new InvalidOperationException($"Navigation configuration for {audience} contains a cycle at '{current.Id}'.");
                }

                current = items[current.ParentId];
            }
        }
    }
}
