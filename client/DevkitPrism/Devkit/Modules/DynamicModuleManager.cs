using System.IO;
using System.Reflection;
using System.Text.Json;
using Devkit.Core.UI.Services;
using Devkit.Prism.Modules;
using Devkit.Services.Interfaces;
using Devkit.Services.Interfaces.Logging;

namespace Devkit.Modules;

public sealed class DynamicModuleManager
{
    private const string StorageModuleName = "module-management";
    private const string CatalogFileName = "catalog.json";
    private readonly object _gate = new();
    private readonly IPrismModuleLoader _loader;
    private readonly IMenuRegistry _menuRegistry;
    private readonly IModuleStorage _storage;
    private readonly IClientLogger _logger;
    private readonly Dictionary<string, DynamicModuleDescriptor> _modules = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IPrismModuleHandle> _loadedModules = new(StringComparer.OrdinalIgnoreCase);
    private ModuleCatalogState _state = new();
    private bool _initialized;

    public DynamicModuleManager(
        IPrismModuleLoader loader,
        IMenuRegistry menuRegistry,
        IModuleStorage storage,
        IClientLogger logger)
    {
        _loader = loader;
        _menuRegistry = menuRegistry;
        _storage = storage;
        _logger = logger;
        ModuleDirectory = Path.Combine(AppContext.BaseDirectory, "modules");
    }

    public event EventHandler? Changed;

    public string ModuleDirectory { get; }

    public IReadOnlyList<DynamicModuleDescriptor> Modules
    {
        get
        {
            lock (_gate)
            {
                return _modules.Values.OrderBy(module => module.ModuleId).ToArray();
            }
        }
    }

    public void Initialize()
    {
        lock (_gate)
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            _state = ReadState();
        }

        Refresh();
        foreach (var module in Modules.Where(module =>
                     module.IsAvailable && !_state.DisabledModuleIds.Contains(module.ModuleId, StringComparer.OrdinalIgnoreCase)))
        {
            try
            {
                LoadCore(module);
            }
            catch (Exception exception)
            {
                RecordFailure(module, exception, "自动加载失败");
            }
        }

        RaiseChanged();
    }

    public void Refresh()
    {
        Directory.CreateDirectory(ModuleDirectory);
        var discoveredPaths = DiscoverBuiltInModulePaths()
            .Select(path => (Path: path, IsExternal: false))
            .Concat(_state.ExternalModulePaths
                .Where(File.Exists)
                .Select(path => (Path: path, IsExternal: true)))
            .GroupBy(item => Path.GetFullPath(item.Path), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

        lock (_gate)
        {
            foreach (var descriptor in _modules.Values.Where(module => !module.IsLoaded))
            {
                descriptor.IsAvailable = false;
                descriptor.Status = "文件不存在";
                descriptor.ErrorMessage = $"找不到模块文件：{descriptor.AssemblyPath}";
            }
        }

        foreach (var candidate in discoveredPaths)
        {
            try
            {
                AddOrUpdateDescriptor(candidate.Path, candidate.IsExternal);
            }
            catch (Exception exception)
            {
                var moduleId = Path.GetFileNameWithoutExtension(candidate.Path);
                lock (_gate)
                {
                    if (!_modules.TryGetValue(moduleId, out var invalid))
                    {
                        invalid = new DynamicModuleDescriptor(moduleId, candidate.Path, candidate.IsExternal);
                        _modules[moduleId] = invalid;
                    }

                    if (!invalid.IsLoaded)
                    {
                        invalid.IsAvailable = false;
                        invalid.Status = "程序集无效";
                        invalid.ErrorMessage = exception.Message;
                    }
                }

                _logger.Warning(
                    exception,
                    "Dynamic module candidate {AssemblyPath} could not be inspected.",
                    candidate.Path);
            }
        }

        foreach (var externalPath in _state.ExternalModulePaths.Where(path => !File.Exists(path)))
        {
            var moduleId = Path.GetFileNameWithoutExtension(externalPath);
            lock (_gate)
            {
                if (!_modules.TryGetValue(moduleId, out var missing))
                {
                    missing = new DynamicModuleDescriptor(moduleId, externalPath, isExternal: true);
                    _modules[moduleId] = missing;
                }
                else if (!string.Equals(
                             Path.GetFullPath(missing.AssemblyPath),
                             Path.GetFullPath(externalPath),
                             StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                missing.IsAvailable = false;
                missing.Status = "文件不存在";
                missing.ErrorMessage = $"找不到模块文件：{externalPath}";
            }
        }

        RaiseChanged();
    }

    public async Task<DynamicModuleDescriptor> AddAndLoadExternalAsync(string assemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        var fullPath = Path.GetFullPath(assemblyPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("模块文件不存在。", fullPath);
        }

        var descriptor = AddOrUpdateDescriptor(fullPath, isExternal: !IsUnderModuleDirectory(fullPath));
        if (descriptor.IsExternal && !_state.ExternalModulePaths.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
        {
            _state.ExternalModulePaths.Add(fullPath);
            SaveState();
        }

        await LoadAsync(descriptor.ModuleId);
        return descriptor;
    }

    public Task LoadAsync(string moduleId)
    {
        var descriptor = Find(moduleId);
        _state.DisabledModuleIds.RemoveAll(id => string.Equals(id, moduleId, StringComparison.OrdinalIgnoreCase));
        SaveState();

        try
        {
            LoadCore(descriptor);
            RaiseChanged();
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            RecordFailure(descriptor, exception, "加载失败");
            throw;
        }
    }

    public async Task UnloadAsync(string moduleId, bool disableAutoLoad = true)
    {
        var descriptor = Find(moduleId);
        IPrismModuleHandle? handle;
        lock (_gate)
        {
            _loadedModules.Remove(moduleId, out handle);
        }

        if (disableAutoLoad && !_state.DisabledModuleIds.Contains(moduleId, StringComparer.OrdinalIgnoreCase))
        {
            _state.DisabledModuleIds.Add(moduleId);
            SaveState();
        }

        if (handle == null)
        {
            descriptor.IsLoaded = false;
            descriptor.Status = disableAutoLoad ? "已禁用" : "未加载";
            RaiseChanged();
            return;
        }

        WeakReference? unloadReference = null;
        Exception? unloadError = null;
        try
        {
            unloadReference = handle.Unload();
        }
        catch (Exception exception)
        {
            unloadError = exception;
            try
            {
                unloadReference = handle.Unload();
            }
            catch (Exception secondaryException)
            {
                unloadError = new AggregateException(exception, secondaryException);
            }
        }
        finally
        {
            _menuRegistry.UnregisterByModule(moduleId);
            try
            {
                handle.Dispose();
            }
            catch (Exception exception)
            {
                unloadError = unloadError == null
                    ? exception
                    : new AggregateException(unloadError, exception);
            }
        }

        descriptor.IsLoaded = false;
        descriptor.ErrorMessage = unloadError?.Message;
        descriptor.Status = unloadError == null ? "已卸载" : "已卸载（清理回调失败）";

        if (unloadReference != null)
        {
            for (var attempt = 0; unloadReference.IsAlive && attempt < 10; attempt++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                await Task.Yield();
            }

            if (unloadReference.IsAlive && unloadError == null)
            {
                descriptor.Status = "已卸载（等待内存回收）";
            }
        }

        RaiseChanged();
        if (unloadError != null)
        {
            throw new InvalidOperationException(
                $"模块“{moduleId}”已卸载，但一个或多个清理回调失败。",
                unloadError);
        }
    }

    public object Resolve(string moduleId, string viewName)
    {
        lock (_gate)
        {
            if (!_loadedModules.TryGetValue(moduleId, out var handle))
            {
                throw new InvalidOperationException($"模块“{moduleId}”当前未加载。");
            }

            return handle.Resolve(viewName);
        }
    }

    public void Shutdown()
    {
        IPrismModuleHandle[] handles;
        lock (_gate)
        {
            handles = _loadedModules.Values.ToArray();
            _loadedModules.Clear();
        }

        foreach (var handle in handles)
        {
            try
            {
                handle.Unload();
            }
            catch (Exception exception)
            {
                _logger.Warning(exception, "Module {ModuleId} could not be cleanly unloaded during shutdown.", handle.ModuleId);
            }
            finally
            {
                _menuRegistry.UnregisterByModule(handle.ModuleId);
                handle.Dispose();
            }
        }
    }

    private void LoadCore(DynamicModuleDescriptor descriptor)
    {
        lock (_gate)
        {
            if (_loadedModules.ContainsKey(descriptor.ModuleId))
            {
                return;
            }
        }

        if (!File.Exists(descriptor.AssemblyPath))
        {
            throw new FileNotFoundException("模块文件不存在。", descriptor.AssemblyPath);
        }

        descriptor.Status = "正在加载";
        descriptor.ErrorMessage = null;
        var handle = _loader.Load(descriptor.AssemblyPath);
        if (!string.Equals(handle.ModuleId, descriptor.ModuleId, StringComparison.OrdinalIgnoreCase))
        {
            handle.Dispose();
            throw new InvalidOperationException($"模块标识不一致：发现 {descriptor.ModuleId}，实际加载 {handle.ModuleId}。");
        }

        lock (_gate)
        {
            _loadedModules[descriptor.ModuleId] = handle;
        }

        descriptor.IsLoaded = true;
        descriptor.IsAvailable = true;
        descriptor.Version = handle.Version?.ToString();
        descriptor.Status = "已加载";
    }

    private DynamicModuleDescriptor AddOrUpdateDescriptor(string assemblyPath, bool isExternal)
    {
        var fullPath = Path.GetFullPath(assemblyPath);
        var assemblyName = AssemblyName.GetAssemblyName(fullPath);
        var moduleId = assemblyName.Name ?? Path.GetFileNameWithoutExtension(fullPath);

        lock (_gate)
        {
            if (!_modules.TryGetValue(moduleId, out var descriptor))
            {
                descriptor = new DynamicModuleDescriptor(moduleId, fullPath, isExternal);
                _modules[moduleId] = descriptor;
            }
            else if (!descriptor.IsLoaded)
            {
                descriptor.AssemblyPath = fullPath;
                descriptor.IsExternal = isExternal;
            }

            descriptor.IsAvailable = true;
            descriptor.Version = assemblyName.Version?.ToString();
            if (!descriptor.IsLoaded)
            {
                descriptor.Status = _state.DisabledModuleIds.Contains(moduleId, StringComparer.OrdinalIgnoreCase)
                    ? "已禁用"
                    : "未加载";
                descriptor.ErrorMessage = null;
            }

            return descriptor;
        }
    }

    private IEnumerable<string> DiscoverBuiltInModulePaths()
    {
        foreach (var directory in Directory.EnumerateDirectories(ModuleDirectory))
        {
            var conventionalPath = Path.Combine(directory, $"{Path.GetFileName(directory)}.dll");
            if (File.Exists(conventionalPath))
            {
                yield return conventionalPath;
                continue;
            }

            var fallback = Directory.EnumerateFiles(directory, "Devkit.Modules.*.dll", SearchOption.TopDirectoryOnly)
                .FirstOrDefault();
            if (fallback != null)
            {
                yield return fallback;
            }
        }

        foreach (var modulePath in Directory.EnumerateFiles(ModuleDirectory, "Devkit.Modules.*.dll", SearchOption.TopDirectoryOnly))
        {
            yield return modulePath;
        }
    }

    private DynamicModuleDescriptor Find(string moduleId)
    {
        lock (_gate)
        {
            return _modules.TryGetValue(moduleId, out var descriptor)
                ? descriptor
                : throw new KeyNotFoundException($"未发现模块“{moduleId}”。");
        }
    }

    private bool IsUnderModuleDirectory(string path)
    {
        var relative = Path.GetRelativePath(ModuleDirectory, path);
        return relative != ".." && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private ModuleCatalogState ReadState()
    {
        var path = _storage.GetFilePath(StorageModuleName, CatalogFileName);
        if (!File.Exists(path))
        {
            return new ModuleCatalogState();
        }

        try
        {
            return JsonSerializer.Deserialize<ModuleCatalogState>(File.ReadAllText(path)) ?? new ModuleCatalogState();
        }
        catch (Exception exception)
        {
            _logger.Warning(exception, "Dynamic module state could not be read; defaults will be used.");
            return new ModuleCatalogState();
        }
    }

    private void SaveState()
    {
        var path = _storage.GetFilePath(StorageModuleName, CatalogFileName);
        var json = JsonSerializer.Serialize(_state, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private void RecordFailure(DynamicModuleDescriptor descriptor, Exception exception, string status)
    {
        _menuRegistry.UnregisterByModule(descriptor.ModuleId);
        descriptor.IsLoaded = false;
        descriptor.Status = status;
        descriptor.ErrorMessage = exception.Message;
        _logger.Error(exception, "Dynamic module {ModuleId} could not be loaded from {AssemblyPath}.", descriptor.ModuleId, descriptor.AssemblyPath);
        RaiseChanged();
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private sealed class ModuleCatalogState
    {
        public List<string> DisabledModuleIds { get; init; } = [];

        public List<string> ExternalModulePaths { get; init; } = [];
    }
}
