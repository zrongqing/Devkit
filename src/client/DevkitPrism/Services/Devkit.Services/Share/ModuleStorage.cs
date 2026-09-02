using Devkit.Services.Interfaces;

namespace Devkit.Services;

public class ModuleStorage : IModuleStorage
{
    private readonly string _rootPath;

    public ModuleStorage()
    {
        _rootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Modules");
        Directory.CreateDirectory(_rootPath);
    }

    #region IModuleStorage Members
    public string GetModulePath(string moduleName)
    {
        var path = Path.Combine(_rootPath, moduleName);
        Directory.CreateDirectory(path);
        return path;
    }

    public string GetFilePath(string moduleName, string fileName)
    {
        return Path.Combine(GetModulePath(moduleName), fileName);
    }
    #endregion
}
