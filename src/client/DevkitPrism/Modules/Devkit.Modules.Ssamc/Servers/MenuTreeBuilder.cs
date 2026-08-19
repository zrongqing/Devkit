using Ssamc.Models;

namespace Ssamc.Servers;

public static class MenuTreeBuilder
{
    public static IReadOnlyList<MenuTreeItem> Build(IEnumerable<MenuTreeRecord> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var records = source
            .GroupBy(record => record.MenuId)
            .Select(group => group.First())
            .ToDictionary(record => record.MenuId);
        var drafts = records.Values.ToDictionary(record => record.MenuId, record => new Draft(record));
        var roots = new List<Draft>();

        foreach (var draft in Sort(drafts.Values))
        {
            var parentId = draft.Record.ParentMenuId;
            if (parentId is > 0 &&
                parentId != draft.Record.MenuId &&
                drafts.TryGetValue(parentId.Value, out var parent) &&
                !HasCycle(draft.Record.MenuId, parentId.Value, drafts))
            {
                parent.Children.Add(draft);
            }
            else
            {
                roots.Add(draft);
            }
        }

        return Sort(roots)
            .Select(root => CreateItem(root, records))
            .ToArray();
    }

    private static bool HasCycle(
        long menuId,
        long parentId,
        IReadOnlyDictionary<long, Draft> drafts)
    {
        var visited = new HashSet<long> { menuId };
        long? currentId = parentId;

        while (currentId is > 0 && drafts.TryGetValue(currentId.Value, out var current))
        {
            if (!visited.Add(currentId.Value))
            {
                return true;
            }

            currentId = current.Record.ParentMenuId;
        }

        return false;
    }

    private static MenuTreeItem CreateItem(
        Draft draft,
        IReadOnlyDictionary<long, MenuTreeRecord> records)
    {
        var record = draft.Record;
        var name = FirstNotEmpty(record.Name, record.EnglishName) ?? record.MenuId.ToString();
        var parentName = record.ParentMenuId is { } parentId &&
                         records.TryGetValue(parentId, out var parent)
                             ? FirstNotEmpty(parent.Name, parent.EnglishName)
                             : null;

        return new MenuTreeItem
        {
            MenuId = record.MenuId,
            ParentMenuId = record.ParentMenuId,
            Name = name,
            Code = record.Code,
            ParentName = parentName,
            ModuleId = record.ModuleId,
            ModuleName = record.ModuleName,
            MainPageId = record.MainPageId,
            MainPageName = record.MainPageName,
            SortOrder = record.SortOrder,
            Children = Sort(draft.Children)
                .Select(child => CreateItem(child, records))
                .ToArray()
        };
    }

    private static IOrderedEnumerable<Draft> Sort(IEnumerable<Draft> drafts)
    {
        return drafts.OrderBy(draft => draft.Record.SortOrder ?? short.MaxValue)
            .ThenBy(draft => draft.Record.MenuId);
    }

    private static string? FirstNotEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }

    #region Nested type: Draft
    private sealed class Draft(MenuTreeRecord record)
    {
        public MenuTreeRecord Record { get; } = record;

        public List<Draft> Children { get; } = [];
    }
    #endregion
}
