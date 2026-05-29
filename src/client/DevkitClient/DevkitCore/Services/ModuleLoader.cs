using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Devkit.Contracts;
using Devkit.Sharp.Contracts;

namespace Devkit.Services;

/// <summary>
/// 轻量模块加载器：扫描指定目录并调用 IModule.RegisterServices
/// 用法：在 App.ConfigureServices 中调用 ModuleLoader.DiscoverAndRegisterModules(...)
/// </summary>
public static class ModuleLoader
{
    /// <summary>
    /// 扫描 modulesPath 下的 dll（不递归子目录），寻找实现 IModule 的类型并调用 RegisterServices。
    /// modulesPath 可以是相对或绝对路径。不会立即实例化模块（只要类型实例化以调用 RegisterServices）。
    /// </summary>
    public static IList<Type> DiscoverAndRegisterModules(string modulesPath, IServiceCollection services, IConfiguration configuration)
    {
        var registeredModuleTypes = new List<Type>();

        if (string.IsNullOrWhiteSpace(modulesPath) || !Directory.Exists(modulesPath))
            return registeredModuleTypes;

        var dllFiles = Directory.GetFiles(modulesPath, "*.dll", SearchOption.TopDirectoryOnly);
        foreach (var dll in dllFiles)
        {
            try
            {
                var asm = Assembly.LoadFrom(dll);
                var moduleTypes = asm.GetTypes().Where(t => typeof(IModule).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass);
                foreach (var t in moduleTypes)
                {
                    // 先用 Activator 创建实例并调用 RegisterServices
                    var module = Activator.CreateInstance(t) as IModule;
                    module?.RegisterServices(services, configuration);
                    registeredModuleTypes.Add(t);
                }
            }
            catch
            {
                // 忽略不可加载 dll（可扩展为日志记录）
            }
        }

        return registeredModuleTypes;
    }

    /// <summary>
    /// 在 Host 启动后，给已经注册的模块调用 OnInitializedAsync。
    /// </summary>
    public static async System.Threading.Tasks.Task InitializeModulesAsync(IEnumerable<Type> moduleTypes, IServiceProvider provider)
    {
        if (moduleTypes == null) return;

        foreach (var t in moduleTypes)
        {
            try
            {
                var module = provider.GetService(t) as IModule ?? ActivatorUtilities.CreateInstance(provider, t) as IModule;
                if (module != null)
                {
                    await module.OnInitializedAsync(provider).ConfigureAwait(false);
                }
            }
            catch
            {
                // 忽略单个模块初始化错误（可扩展为日志）
            }
        }
    }
}
