using Barcode2.Models;
using Barcode2.Servers;
using Xunit;

namespace Devkit.Loading.Tests;

public sealed class MenuTreeBuilderTests
{
    [Fact]
    public void Id_and_parent_id_form_a_sorted_tree_with_module_details()
    {
        var result = MenuTreeBuilder.Build(
        [
            Record(10, name: "Second root", sort: 2),
            Record(20, name: "First root", sort: 1),
            Record(
                11,
                parentId: 10,
                name: "Child",
                sort: 1,
                moduleId: 9001,
                moduleName: "Child module",
                mainPageId: 9101,
                mainPageName: "Child main page")
        ]);

        Assert.Equal(new long[] { 20, 10 }, result.Select(item => item.MenuId));
        var child = Assert.Single(result[1].Children);
        Assert.Equal("Second root", child.ParentName);
        Assert.Equal(9001, child.ModuleId);
        Assert.Equal("Child module", child.ModuleName);
        Assert.Equal(9101, child.MainPageId);
        Assert.Equal("Child main page", child.MainPageName);
    }

    [Fact]
    public void Orphans_self_references_and_cycles_are_promoted_without_losing_records()
    {
        var result = MenuTreeBuilder.Build(
        [
            Record(1, parentId: 999, name: "Orphan"),
            Record(2, parentId: 2, name: "Self"),
            Record(3, parentId: 4, name: "Cycle A"),
            Record(4, parentId: 3, name: "Cycle B")
        ]);

        var allIds = Flatten(result).Select(item => item.MenuId).Order().ToArray();
        Assert.Equal(new long[] { 1, 2, 3, 4 }, allIds);
        Assert.Equal(4, result.Count);
    }

    [Fact]
    public void Empty_name_falls_back_to_english_name_then_id()
    {
        var result = MenuTreeBuilder.Build(
        [
            new MenuTreeRecord { MenuId = 1, EnglishName = "English" },
            new MenuTreeRecord { MenuId = 2 }
        ]);

        Assert.Equal("English", result[0].Name);
        Assert.Equal("2", result[1].Name);
    }

    private static MenuTreeRecord Record(
        long id,
        long? parentId = null,
        string? name = null,
        short? sort = null,
        long? moduleId = null,
        string? moduleName = null,
        long? mainPageId = null,
        string? mainPageName = null)
    {
        return new MenuTreeRecord
        {
            MenuId = id,
            ParentMenuId = parentId,
            Name = name,
            SortOrder = sort,
            ModuleId = moduleId,
            ModuleName = moduleName,
            MainPageId = mainPageId,
            MainPageName = mainPageName
        };
    }

    private static IEnumerable<MenuTreeItem> Flatten(IEnumerable<MenuTreeItem> items)
    {
        foreach (var item in items)
        {
            yield return item;
            foreach (var child in Flatten(item.Children))
            {
                yield return child;
            }
        }
    }
}
