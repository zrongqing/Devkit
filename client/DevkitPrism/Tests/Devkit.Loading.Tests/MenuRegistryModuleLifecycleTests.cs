using Devkit.Core.UI.Attributes;
using Devkit.Core.UI.Models;
using Devkit.Core.UI.Services;
using Moq;
using Prism.Ioc;
using Xunit;

namespace Devkit.Loading.Tests;

public sealed class MenuRegistryModuleLifecycleTests
{
    [Fact]
    public void UnregisterByModule_removes_only_owned_menus_and_notifies()
    {
        var registry = new MenuRegistry(Mock.Of<IContainerProvider>());
        var changeCount = 0;
        registry.Changed += (_, _) => changeCount++;
        registry.Register(new MenuItemModel { Id = "host", Title = "Host" });
        registry.Register(new MenuItemModel { Id = "module", Title = "Module", ModuleId = "Test.Module" });

        registry.UnregisterByModule("test.module");

        Assert.NotNull(registry.Find("host"));
        Assert.Null(registry.Find("module"));
        Assert.Equal(3, changeCount);
    }

    [Fact]
    public void ScanFromAssembly_assigns_module_owner()
    {
        var registry = new MenuRegistry(Mock.Of<IContainerProvider>());

        registry.ScanFromAssembly(typeof(AttributedTestView).Assembly, "Test.Module");

        var menu = registry.Find("tests.dynamic.menu");
        Assert.NotNull(menu);
        Assert.Equal("Test.Module", menu.ModuleId);
    }

    [MenuItem(Id = "tests.dynamic.menu", Title = "Dynamic test menu")]
    private sealed class AttributedTestView
    {
    }
}
