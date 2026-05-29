//using Devkit.Contracts;
//using Microsoft.Extensions.Configuration;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.DependencyInjection.Extensions;
//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Reflection;
//using System.Runtime.Loader;
//using System.Threading.Tasks;

//namespace Devkit.Services;

//public class ModuleLoadContext : AssemblyLoadContext
//{
//    private readonly string _modulePath;

//    public ModuleLoadContext(string modulePath) : base(isCollectible: true)
//    {
//        _modulePath = Path.GetDirectoryName(modulePath);
//    }

//    protected override Assembly Load(AssemblyName assemblyName)
//    {
//        // 优先从模块目录加载依赖
//        var candidate = Path.Combine(_modulePath, assemblyName.Name + ".dll");
//        if (File.Exists(candidate))
//        {
//            return LoadFromAssemblyPath(candidate);
//        }

//        // 回退到默认上下文
//        return null;
//    }
//}

///// <summary>
///// 支持可卸载模块：为每个模块创建可回收的 AssemblyLoadContext 和独立 ServiceProvider。
///// </summary>
//public static class ModuleLoader
//{
//    public record ModuleInstance(string Name, string Path, ModuleLoadContext LoadContext, IServiceProvider ServiceProvider, IModule Module);

//    // 记录已加载的模块
//    private static readonly ConcurrentDictionary<string, ModuleInstance> _loaded = new();

//    // 发现并加载模块（返回 ModuleInstance 列表）
//    public static IList<ModuleInstance> DiscoverAndLoadModules(string modulesPath, IConfiguration configuration)
//    {
//        var list = new List<ModuleInstance>();
//        if (string.IsNullOrWhiteSpace(modulesPath) || !Directory.Exists(modulesPath)) return list;

//        var dllFiles = Directory.GetFiles(modulesPath, "*.dll", SearchOption.TopDirectoryOnly);
//        foreach (var dll in dllFiles)
//        {
//            try
//            {
//                var alc = new ModuleLoadContext(dll);
//                var asm = alc.LoadFromAssemblyPath(dll);
//                var moduleTypes = asm.GetTypes().Where(t => typeof(IModule).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass);
//                foreach (var t in moduleTypes)
//                {
//                    // 为模块创建独立的 IServiceCollection / ServiceProvider
//                    var services = new ServiceCollection();

//                    // 允许模块引用宿主契约（例如 IPageService 的接口），这些实现来自宿主，需要注入到模块 provider
//                    // 将宿主服务的实例占位注入为工厂，以免把模块类型直接写入宿主容器
//                    services.TryAddSingleton(typeof(IServiceProvider), sp => sp);

//                    // 创建模块实例并调用 RegisterServices（模块会向这个 services 注册类型）
//                    var module = (IModule)Activator.CreateInstance(t);
//                    module.RegisterServices(services, configuration);

//                    // Build module provider
//                    var moduleProvider = services.BuildServiceProvider();

//                    // 记录模块实例
//                    var mi = new ModuleInstance(t.FullName, dll, alc, moduleProvider, module);
//                    _loaded[mi.Name] = mi;

//                    // 在 Host 启动后调用 OnInitializedAsync，需要宿主在合适时机使用 moduleProvider 调用
//                    list.Add(mi);
//                }
//            }
//            catch
//            {
//                // 忽略单个模块加载失败（实际应日志）
//            }
//        }

//        return list;
//    }

//    // 运行时初始化某个已加载模块（宿主应在 StartAsync 后调用）
//    public static async Task InitializeModuleAsync(ModuleInstance instance, IServiceProvider hostServiceProvider)
//    {
//        if (instance == null) return;

//        // 将宿主服务注入到模块 provider（如果模块需要宿主契约，模块在 RegisterServices 时可以从 provider 请求宿主服务）
//        // 通过创建一个 ServiceScope 并传入 hostServiceProvider 以便模块获取宿主服务：
//        // 若模块需要直接访问宿主服务，请在 RegisterServices 中将 factories 绑定到 hostServiceProvider。
//        try
//        {
//            await instance.Module.OnInitializedAsync(instance.ServiceProvider).ConfigureAwait(false);
//        }
//        catch
//        {
//            // 忽略初始化错误（应记录）
//        }
//    }

//    // 卸载模块：先调用模块提供的清理（如果有），然后 Dispose Provider，Unload ALC，并等待回收
//    public static async Task<bool> UnloadModuleAsync(string moduleName, IServiceProvider hostServices, TimeSpan? timeout = null)
//    {
//        if (!_loaded.TryRemove(moduleName, out var instance)) return false;

//        try
//        {
//            // 如果模块实现了一个清理接口或在 OnInitializedAsync 注册了回调，请先让模块做清理
//            if (instance.Module is IDisposable disposableModule)
//            {
//                disposableModule.Dispose();
//            }

//            // 模块可能在宿主注册了页面映射等，需要宿主主动移除（示例：PageService）
//            var pageService = hostServices.GetService(typeof(Devkit.Contracts.Services.IPageService)) as Devkit.Contracts.Services.IPageService;
//            if (pageService != null)
//            {
//                // 这里使用反射调用 PageService.UnregisterPageForViewModel，如果宿主实现了该方法
//                var unregister = pageService.GetType().GetMethod("UnregisterPageForViewModel");
//                if (unregister != null)
//                {
//                    // 如果模块  OnInitializedAsync 时注册了映射，模块类型的 ViewModel Type 位于 module assembly 中，
//                    // 需要模块提供要注销的 ViewModel 类型名称或在模块实例中保存已注册的映射信息。
//                    // 这里仅演示场景：模块暴露了要注销的 VM 全名（需模块配合）。
//                }
//            }

//            // Dispose 模块 ServiceProvider（释放模块持有的托管资源）
//            if (instance.ServiceProvider is IDisposable disp)
//            {
//                disp.Dispose();
//            }

//            // 请求卸载 ALC
//            instance.LoadContext.Unload();

//            // 等待 GC 回收 ALC（弱引用检测）
//            var weak = new WeakReference(instance.LoadContext);
//            instance = null;

//            var t = timeout ?? TimeSpan.FromSeconds(5);
//            var sw = System.Diagnostics.Stopwatch.StartNew();
//            while (weak.IsAlive && sw.Elapsed < t)
//            {
//                GC.Collect();
//                GC.WaitForPendingFinalizers();
//                await Task.Delay(200).ConfigureAwait(false);
//            }

//            return !weak.IsAlive;
//        }
//        catch
//        {
//            return false;
//        }
//    }

//    public static IEnumerable<ModuleInstance> GetLoadedModules() => _loaded.Values;
//}