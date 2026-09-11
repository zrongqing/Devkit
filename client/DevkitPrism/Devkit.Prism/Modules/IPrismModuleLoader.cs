namespace Devkit.Prism.Modules;

public interface IPrismModuleLoader
{
    IPrismModuleHandle Load(string assemblyPath);
}

public interface IPrismModuleHandle : IDisposable
{
    string ModuleId { get; }

    string AssemblyPath { get; }

    Version? Version { get; }

    object Resolve(string name);

    WeakReference Unload();
}
