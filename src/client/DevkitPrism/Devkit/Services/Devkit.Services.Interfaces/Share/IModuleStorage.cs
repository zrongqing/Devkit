namespace Devkit.Services.Interfaces;

public interface IModuleStorage
{
    /// <summary>
    /// 获取某个模块的存储目录（自动创建）
    /// </summary>
    string GetModulePath(string moduleName);

    /// <summary>
    /// 获取某模块下的文件路径
    /// </summary>
    string GetFilePath(string moduleName, string fileName);
}