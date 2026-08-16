using System.Runtime.CompilerServices;
using Devkit.Core.UI.Models;
using Devkit.Core.UI.Services;
using Devkit.Modules.Demo;
using Devkit.Prism.Modules;
using DryIoc;
using Prism.Container.DryIoc;
using Prism.Ioc;
using Xunit;

namespace Devkit.Loading.Tests;

public sealed class PrismModuleLoaderTests
{
    [Fact]
    public void Module_is_loaded_in_collectible_context_and_runs_unload_hook()
    {
        using var root = new Container();
        var rootExtension = new DryIocContainerExtension(root);
        var menuRegistry = new MenuRegistry(rootExtension);
        rootExtension.RegisterInstance<IContainerProvider>(rootExtension);
        rootExtension.RegisterInstance<IMenuRegistry>(menuRegistry);
        var loader = new PrismModuleLoader(rootExtension);

        var unloadReference = LoadAndUnload(loader, menuRegistry, typeof(DemoModule).Assembly.Location);

        Assert.Null(menuRegistry.Find("demo"));
        Assert.Null(menuRegistry.Find("modules.demo.loading"));
        AssertCollectible(unloadReference);
    }

    [Fact]
    public void Cleanup_error_does_not_prevent_container_and_load_context_unload()
    {
        using var root = new Container();
        var rootExtension = new DryIocContainerExtension(root);
        var menuRegistry = new MenuRegistry(rootExtension);
        rootExtension.RegisterInstance<IContainerProvider>(rootExtension);
        rootExtension.RegisterInstance<IMenuRegistry>(menuRegistry);
        var loader = new PrismModuleLoader(rootExtension);

        var unloadReference = LoadThrowingCleanupModuleAndUnload(
            loader,
            menuRegistry,
            typeof(PrismModuleLoaderTests).Assembly.Location);

        Assert.Null(menuRegistry.Find("tests.throwing-cleanup"));
        AssertCollectible(unloadReference);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference LoadAndUnload(
        IPrismModuleLoader loader,
        IMenuRegistry menuRegistry,
        string modulePath)
    {
        using var handle = loader.Load(modulePath);
        Assert.Equal("Devkit.Modules.Demo", handle.ModuleId);
        Assert.NotNull(menuRegistry.Find("demo"));
        Assert.NotNull(menuRegistry.Find("modules.demo.loading"));
        return handle.Unload();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference LoadThrowingCleanupModuleAndUnload(
        IPrismModuleLoader loader,
        IMenuRegistry menuRegistry,
        string modulePath)
    {
        var handle = loader.Load(modulePath);
        Assert.Equal("Devkit.Loading.Tests", handle.ModuleId);
        Assert.NotNull(menuRegistry.Find("tests.throwing-cleanup"));
        Assert.Throws<AggregateException>(() => handle.Unload());
        return handle.Unload();
    }

    private static void AssertCollectible(WeakReference unloadReference)
    {
        for (var attempt = 0; unloadReference.IsAlive && attempt < 10; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        Assert.False(unloadReference.IsAlive);
    }

    public sealed class ThrowingCleanupModule : IModule, IUnloadableModule
    {
        private const string ModuleId = "Devkit.Loading.Tests";

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
            containerProvider.Resolve<IMenuRegistry>().Register(new MenuItemModel
            {
                Id = "tests.throwing-cleanup",
                ModuleId = ModuleId,
                Title = "Throwing cleanup test"
            });
        }

        public void OnUnloading(IContainerProvider containerProvider)
        {
            containerProvider.Resolve<IMenuRegistry>().UnregisterByModule(ModuleId);
            throw new InvalidOperationException("Expected cleanup failure.");
        }
    }
}
