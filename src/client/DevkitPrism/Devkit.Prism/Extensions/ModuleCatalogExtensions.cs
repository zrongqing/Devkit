using System.Reflection;
using System.Runtime.Loader;
using System.IO;
using Prism.Modularity;

namespace Devkit.Prism.Extensions;

public static class ModuleCatalogExtensions
{
    public static void AddModulesFromDirectory(this IModuleCatalog moduleCatalog, string modulesPath)
    {
        Directory.CreateDirectory(modulesPath);

        foreach (var moduleAssemblyPath in Directory.EnumerateFiles(modulesPath, "Devkit.Modules.*.dll", SearchOption.AllDirectories))
        {
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(moduleAssemblyPath));

            foreach (var moduleType in GetModuleTypes(assembly))
            {
                moduleCatalog.AddModule(new ModuleInfo
                {
                    ModuleName = moduleType.FullName!,
                    ModuleType = moduleType.AssemblyQualifiedName!,
                    InitializationMode = InitializationMode.WhenAvailable
                });
            }
        }
    }

    private static IEnumerable<Type> GetModuleTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes().Where(type => !type.IsAbstract && typeof(IModule).IsAssignableFrom(type));
        }
        catch (ReflectionTypeLoadException exception)
        {
            var loaderErrors = string.Join(Environment.NewLine, exception.LoaderExceptions.Select(error => error?.Message));
            throw new InvalidOperationException($"Unable to load Prism module assembly '{assembly.FullName}'.{Environment.NewLine}{loaderErrors}", exception);
        }
    }
}
