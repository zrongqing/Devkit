using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace Devkit.Prism.Modules;

internal sealed class PrismModuleLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _hostDirectory;
    private readonly HashSet<string> _hostAssemblies;

    public PrismModuleLoadContext(string assemblyPath, string hostDirectory)
        : base($"DevkitModule:{Path.GetFileNameWithoutExtension(assemblyPath)}", isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(assemblyPath);
        _hostDirectory = hostDirectory;
        _hostAssemblies = Directory
            .EnumerateFiles(hostDirectory, "*.dll", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase)!;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (!string.IsNullOrWhiteSpace(assemblyName.Name) && _hostAssemblies.Contains(assemblyName.Name))
        {
            var loaded = Default.Assemblies.FirstOrDefault(candidate =>
                AssemblyName.ReferenceMatchesDefinition(assemblyName, candidate.GetName()));
            if (loaded != null)
            {
                return loaded;
            }

            var hostPath = Path.Combine(_hostDirectory, $"{assemblyName.Name}.dll");
            if (File.Exists(hostPath))
            {
                var hostAssemblyName = AssemblyName.GetAssemblyName(hostPath);
                if (!AssemblyName.ReferenceMatchesDefinition(assemblyName, hostAssemblyName))
                {
                    throw new FileLoadException(
                        $"The host assembly '{hostAssemblyName.FullName}' is not compatible with '{assemblyName.FullName}'.",
                        hostPath);
                }

                return Default.LoadFromAssemblyPath(hostPath);
            }
        }

        var dependencyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        return dependencyPath == null ? null : LoadFromAssemblyPath(dependencyPath);
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        var dependencyPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return dependencyPath == null ? nint.Zero : LoadUnmanagedDllFromPath(dependencyPath);
    }
}
