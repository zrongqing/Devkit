using System.IO;
using System.Reflection;
using DryIoc;
using Prism.Container.DryIoc;

namespace Devkit.Prism.Modules;

public sealed class PrismModuleLoader(IContainerProvider rootContainerProvider) : IPrismModuleLoader
{
    public IPrismModuleHandle Load(string assemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        var fullPath = Path.GetFullPath(assemblyPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The module assembly does not exist.", fullPath);
        }

        var rootExtension = rootContainerProvider as IContainerExtension<IContainer>
            ?? throw new InvalidOperationException("The Prism root container is not backed by DryIoc.");
        var childContainer = rootExtension.Instance.CreateChild(
            ifAlreadyRegistered: IfAlreadyRegistered.Replace);
        var childExtension = new DryIocContainerExtension(childContainer);
        PrismModuleLoadContext? loadContext = null;
        var modules = new List<IModule>();

        try
        {
            loadContext = new PrismModuleLoadContext(fullPath, AppContext.BaseDirectory);
            childExtension.RegisterInstance<IContainerProvider>(childExtension);
            childExtension.RegisterInstance<IContainerRegistry>(childExtension);

            var assembly = loadContext.LoadModuleAssembly();
            var moduleTypes = GetModuleTypes(assembly).ToArray();
            if (moduleTypes.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Assembly '{assembly.FullName}' does not contain a concrete Prism IModule implementation.");
            }

            modules = new List<IModule>(moduleTypes.Length);
            foreach (var moduleType in moduleTypes)
            {
                childExtension.Register(moduleType);
                modules.Add((IModule)childExtension.Resolve(moduleType));
            }

            foreach (var module in modules)
            {
                module.RegisterTypes(childExtension);
            }

            foreach (var module in modules)
            {
                module.OnInitialized(childExtension);
            }

            return new PrismModuleHandle(
                assembly.GetName().Name ?? Path.GetFileNameWithoutExtension(fullPath),
                fullPath,
                assembly.GetName().Version,
                loadContext,
                childContainer,
                childExtension,
                modules);
        }
        catch
        {
            for (var index = modules.Count - 1; index >= 0; index--)
            {
                if (modules[index] is not IUnloadableModule unloadable)
                {
                    continue;
                }

                try
                {
                    unloadable.OnUnloading(childExtension);
                }
                catch
                {
                    // Preserve the original load exception while making a best effort to clean up.
                }
            }

            try
            {
                childContainer.Dispose();
            }
            catch
            {
                // Preserve the original load exception.
            }

            try
            {
                loadContext?.Unload();
            }
            catch
            {
                // Preserve the original load exception.
            }

            throw;
        }
    }

    private static IEnumerable<Type> GetModuleTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes()
                .Where(type => type is { IsAbstract: false, IsInterface: false } &&
                               typeof(IModule).IsAssignableFrom(type));
        }
        catch (ReflectionTypeLoadException exception)
        {
            var loaderErrors = string.Join(
                Environment.NewLine,
                exception.LoaderExceptions.Where(error => error != null).Select(error => error!.Message));
            throw new InvalidOperationException(
                $"Unable to inspect Prism module assembly '{assembly.FullName}'.{Environment.NewLine}{loaderErrors}",
                exception);
        }
    }

    private sealed class PrismModuleHandle : IPrismModuleHandle
    {
        private PrismModuleLoadContext? _loadContext;
        private IContainer? _container;
        private IContainerProvider? _containerProvider;
        private List<IModule>? _modules;
        private WeakReference? _unloadReference;

        public PrismModuleHandle(
            string moduleId,
            string assemblyPath,
            Version? version,
            PrismModuleLoadContext loadContext,
            IContainer container,
            IContainerProvider containerProvider,
            List<IModule> modules)
        {
            ModuleId = moduleId;
            AssemblyPath = assemblyPath;
            Version = version;
            _loadContext = loadContext;
            _container = container;
            _containerProvider = containerProvider;
            _modules = modules;
        }

        public string ModuleId { get; }

        public string AssemblyPath { get; }

        public Version? Version { get; }

        public object Resolve(string name)
        {
            ObjectDisposedException.ThrowIf(_containerProvider == null, this);
            return _containerProvider.Resolve<object>(name);
        }

        public WeakReference Unload()
        {
            if (_unloadReference != null)
            {
                return _unloadReference;
            }

            List<Exception>? cleanupErrors = null;
            var modules = _modules;
            var provider = _containerProvider;
            if (modules != null && provider != null)
            {
                for (var index = modules.Count - 1; index >= 0; index--)
                {
                    if (modules[index] is not IUnloadableModule unloadable)
                    {
                        continue;
                    }

                    try
                    {
                        unloadable.OnUnloading(provider);
                    }
                    catch (Exception exception)
                    {
                        (cleanupErrors ??= []).Add(exception);
                    }
                }
            }

            _modules = null;
            _containerProvider = null;
            try
            {
                _container?.Dispose();
            }
            catch (Exception exception)
            {
                (cleanupErrors ??= []).Add(exception);
            }
            finally
            {
                _container = null;
            }

            var loadContext = _loadContext;
            _loadContext = null;
            _unloadReference = new WeakReference(loadContext, trackResurrection: false);
            try
            {
                loadContext?.Unload();
            }
            catch (Exception exception)
            {
                (cleanupErrors ??= []).Add(exception);
            }

            if (cleanupErrors is { Count: > 0 })
            {
                throw new AggregateException(
                    $"Module '{ModuleId}' was unloaded with one or more cleanup errors.",
                    cleanupErrors);
            }

            return _unloadReference;
        }

        public void Dispose() => Unload();
    }
}
