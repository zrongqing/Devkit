using System.Reflection;
using System.Runtime.Loader;
using System.IO;
using Prism.Modularity;

namespace Devkit.Prism.Extensions;

public static class ModuleCatalogExtensions
{
    private static readonly object ResolverLock = new();
    private static readonly HashSet<string> ModuleDirectories = new(StringComparer.OrdinalIgnoreCase);
    private static bool _resolverRegistered;

    public static void AddModulesFromDirectory(this IModuleCatalog moduleCatalog, string modulesPath)
    {
        Directory.CreateDirectory(modulesPath);

        var moduleAssemblyPaths = Directory
            .EnumerateFiles(modulesPath, "*.Modules.*.dll", SearchOption.AllDirectories)
            .Select(Path.GetFullPath)
            .ToArray();

        RegisterModuleDependencyResolver(moduleAssemblyPaths);

        foreach (var moduleAssemblyPath in moduleAssemblyPaths)
        {
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(moduleAssemblyPath);

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

    private static void RegisterModuleDependencyResolver(IEnumerable<string> moduleAssemblyPaths)
    {
        lock (ResolverLock)
        {
            foreach (var moduleAssemblyPath in moduleAssemblyPaths)
            {
                var moduleDirectory = Path.GetDirectoryName(moduleAssemblyPath);
                if (!string.IsNullOrEmpty(moduleDirectory))
                {
                    ModuleDirectories.Add(moduleDirectory);
                }
            }

            if (_resolverRegistered)
            {
                return;
            }

            AssemblyLoadContext.Default.Resolving += ResolveModuleDependency;
            _resolverRegistered = true;
        }
    }

    private static Assembly? ResolveModuleDependency(AssemblyLoadContext loadContext, AssemblyName requestedAssembly)
    {
        string[] moduleDirectories;
        lock (ResolverLock)
        {
            moduleDirectories = ModuleDirectories.ToArray();
        }

        foreach (var moduleDirectory in moduleDirectories)
        {
            var candidatePath = Path.Combine(moduleDirectory, $"{requestedAssembly.Name}.dll");
            if (!File.Exists(candidatePath))
            {
                continue;
            }

            var candidateAssembly = AssemblyName.GetAssemblyName(candidatePath);
            if (!AssemblyName.ReferenceMatchesDefinition(requestedAssembly, candidateAssembly))
            {
                continue;
            }

            return loadContext.LoadFromAssemblyPath(candidatePath);
        }

        return null;
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
