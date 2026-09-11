using CommunityToolkit.Mvvm.ComponentModel;

namespace Devkit.Modules;

public sealed partial class DynamicModuleDescriptor : ObservableObject
{
    public DynamicModuleDescriptor(string moduleId, string assemblyPath, bool isExternal)
    {
        ModuleId = moduleId;
        AssemblyPath = assemblyPath;
        IsExternal = isExternal;
    }

    public string ModuleId { get; }

    public string AssemblyPath { get; internal set; }

    public bool IsExternal { get; internal set; }

    [ObservableProperty]
    private bool _isAvailable = true;

    [ObservableProperty]
    private bool _isLoaded;

    [ObservableProperty]
    private string? _version;

    [ObservableProperty]
    private string _status = "未加载";

    [ObservableProperty]
    private string? _errorMessage;
}
