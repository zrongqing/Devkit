using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using Barcode2;
using Devkit.Core.UI.Models;
using Devkit.Core.UI.Services;
using Devkit.Modules.Demo;
using Devkit.Modules.ModuleName;
using Devkit.Prism.Modules;
using Devkit.Services.Interfaces;
using DryIoc;
using Moq;
using Prism.Container.DryIoc;
using Prism.Ioc;
using Prism.Navigation.Regions;
using Xunit;

namespace Devkit.Loading.Tests;

public sealed class PrismModuleLoaderTests
{
    [Fact]
    public void Barcode2_module_registers_only_semantic_menu_ids()
    {
        using var root = new Container();
        var rootExtension = new DryIocContainerExtension(root);
        var menuRegistry = new MenuRegistry(rootExtension);
        rootExtension.RegisterInstance<IMenuRegistry>(menuRegistry);

        new Barcode2Module().OnInitialized(rootExtension);

        Assert.Equal("Devkit.Modules.Barcode2", typeof(Barcode2Module).Assembly.GetName().Name);
        Assert.NotNull(menuRegistry.Find("barcode2"));
        Assert.NotNull(menuRegistry.Find("barcode2.apiupdate"));
        Assert.NotNull(menuRegistry.Find("barcode2.webappupdate"));
        Assert.NotNull(menuRegistry.Find("barcode2.menusearch"));
        Assert.NotNull(menuRegistry.Find("barcode2.settings"));
    }

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
    public void Source_module_file_remains_exclusively_accessible_while_loaded()
    {
        using var root = new Container();
        var rootExtension = new DryIocContainerExtension(root);
        var menuRegistry = new MenuRegistry(rootExtension);
        rootExtension.RegisterInstance<IContainerProvider>(rootExtension);
        rootExtension.RegisterInstance<IMenuRegistry>(menuRegistry);
        var loader = new PrismModuleLoader(rootExtension);
        using var moduleCopy = TemporaryModuleCopy.Create(typeof(DemoModule).Assembly.Location);
        using var handle = loader.Load(moduleCopy.AssemblyPath);

        using var sourceStream = new FileStream(
            moduleCopy.AssemblyPath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);

        Assert.True(sourceStream.CanRead);
        Assert.True(sourceStream.CanWrite);
    }

    [Fact]
    public void Source_module_file_is_replaceable_after_unloading_a_wpf_view()
    {
        using var root = new Container();
        var rootExtension = new DryIocContainerExtension(root);
        var menuRegistry = new MenuRegistry(rootExtension);
        rootExtension.RegisterInstance<IContainerProvider>(rootExtension);
        rootExtension.RegisterInstance<IMenuRegistry>(menuRegistry);
        rootExtension.RegisterInstance(Mock.Of<IRegionManager>());
        rootExtension.RegisterInstance(Mock.Of<IMessageService>());
        var loader = new PrismModuleLoader(rootExtension);
        using var moduleCopy = TemporaryModuleCopy.Create(typeof(ModuleNameModule).Assembly.Location);
        using var handle = loader.Load(moduleCopy.AssemblyPath);

        ResolveViewOnSta(handle, "ViewA");
        handle.Unload();

        using var sourceStream = new FileStream(
            moduleCopy.AssemblyPath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);
        Assert.True(sourceStream.CanWrite);
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ResolveViewOnSta(IPrismModuleHandle handle, string viewName)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var content = Assert.IsAssignableFrom<FrameworkElement>(handle.Resolve(viewName));
                Assert.Equal(
                    "Devkit.Modules.ModuleName.ViewModels.ViewAViewModel",
                    content.DataContext?.GetType().FullName);
                content.DataContext = null;
            }
            catch (Exception exception)
            {
                error = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error != null)
        {
            throw new InvalidOperationException("The WPF module view could not be resolved.", error);
        }
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

    private sealed class TemporaryModuleCopy : IDisposable
    {
        private TemporaryModuleCopy(string directoryPath, string assemblyPath)
        {
            DirectoryPath = directoryPath;
            AssemblyPath = assemblyPath;
        }

        public string DirectoryPath { get; }

        public string AssemblyPath { get; }

        public static TemporaryModuleCopy Create(string sourceAssemblyPath)
        {
            var directoryPath = Path.Combine(
                Path.GetTempPath(),
                "Devkit.Loading.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);
            var assemblyPath = Path.Combine(directoryPath, Path.GetFileName(sourceAssemblyPath));
            File.Copy(sourceAssemblyPath, assemblyPath);

            foreach (var sidecarPath in new[]
                     {
                         Path.ChangeExtension(sourceAssemblyPath, ".deps.json"),
                         Path.ChangeExtension(sourceAssemblyPath, ".runtimeconfig.json"),
                         $"{sourceAssemblyPath}.config"
                     })
            {
                if (File.Exists(sidecarPath))
                {
                    File.Copy(sidecarPath, Path.Combine(directoryPath, Path.GetFileName(sidecarPath)));
                }
            }

            return new TemporaryModuleCopy(directoryPath, assemblyPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }
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
