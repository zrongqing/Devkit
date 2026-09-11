using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Barcode2.Configuration;

namespace Barcode2.Servers;

/// <summary>
/// Web应用程序更新服务器类
/// 用于将本地webapp文件夹同步到多个目标服务器
/// </summary>
public class WebappUpdateServer
{
    #region Windows API 网络连接
    [DllImport("mpr.dll")]
    private static extern int WNetUseConnection(
        IntPtr hwndOwner,
        NETRESOURCE lpNetResource,
        string? lpPassword,
        string? lpUserID,
        int dwFlags,
        string? lpAccessName,
        string? lpBufferSize,
        string? lpBuffer
    );

    [StructLayout(LayoutKind.Sequential)]
    private class NETRESOURCE
    {
        public int dwScope = 0;
        public int dwType;
        public int dwDisplayType = 0;
        public int dwUsage = 0;
        public string lpLocalName = "";
        public string lpRemoteName = "";
        public string lpComment = "";
        public string lpProvider = "";
    }

    private const int RESOURCETYPE_DISK = 0x1;
    private const int CONNECT_INTERACTIVE = 0x00000008;
    private const int CONNECT_PROMPT = 0x00000010;
    private const int CONNECT_REDIRECT = 0x00000080;
    private const int CONNECT_UPDATE_PROFILE = 0x00000001;
    private const int CONNECT_COMMANDLINE = 0x00000800;
    private const int CONNECT_CMD_SAVECRED = 0x00001000;
    #endregion

    #region 私有字段
    private StringBuilder? _logBuffer;

    private readonly Dictionary<string, List<string>> _destinationFolderDic = new();
    #endregion

    #region 公共属性
    /// <summary>
    /// 源文件夹路径
    /// </summary>
    public string SourceFolder { get; set; }

    /// <summary>
    /// 目标文件夹列表
    /// </summary>
    public List<string> DestinationFolders { get; set; } = [];

    /// <summary>
    /// 网络共享用户名
    /// </summary>
    public string? NetworkUsername { get; set; }

    /// <summary>
    /// 网络共享密码
    /// </summary>
    public string? NetworkPassword { get; set; }

    /// <summary>
    /// 排除的文件夹列表
    /// </summary>
    public List<string> ExcludedFolders { get; set; } = [];

    /// <summary>
    /// 排除的文件列表（支持通配符）
    /// </summary>
    public List<string> ExcludedFiles { get; set; } = [];

    /// <summary>
    /// 日志文件路径
    /// </summary>
    public string? LogFilePath { get; set; }

    /// <summary>
    /// 是否启用日志记录
    /// </summary>
    public bool EnableLogging { get; set; }

    /// <summary>
    /// 是否启用控制台输出
    /// </summary>
    public bool EnableConsoleOutput { get; set; }

    /// <summary>
    /// 模拟模式（只显示要执行的操作，不实际复制）
    /// </summary>
    public bool SimulateMode { get; set; }

    /// <summary>
    /// 最后一次操作的统计信息
    /// </summary>
    public UpdateStatistics? LastStatistics { get; private set; }
    #endregion

    #region 构造函数
    /// <summary>
    /// 默认构造函数
    /// </summary>
    public WebappUpdateServer(string sourceFolder)
    {
        SourceFolder = sourceFolder;
        InitializeDefaultValues();
        InitializeDestination();
    }

    /// <summary>
    /// 带参数的构造函数
    /// </summary>
    public WebappUpdateServer(string sourceFolder, List<string> destinationFolders, string username, string password) : this(sourceFolder)
    {
        SourceFolder = sourceFolder;
        DestinationFolders = destinationFolders ?? new List<string>();
        NetworkUsername = username;
        NetworkPassword = password;
    }

    private void InitializeDestination()
    {
        _destinationFolderDic.Clear();

        var defaults = Barcode2Defaults.Create();
        foreach (var target in defaults.ShareTargets)
        {
            if (!_destinationFolderDic.TryGetValue(target.TargetKey, out var roots))
            {
                roots = [];
                _destinationFolderDic[target.TargetKey] = roots;
            }

            roots.Add(target.Root);
        }

        SourceFolder = defaults.WebappSourcePath;
        DestinationFolders = _destinationFolderDic.TryGetValue("production", out var production)
                                 ? production
                                 : [];
    }


    private void InitializeDefaultValues()
    {
        DestinationFolders = [];

        NetworkUsername = "";
        NetworkPassword = "";

        ExcludedFolders = new List<string>
        {
            "node_modules",
            ".git",
            ".svn",
            ".vs",
            "bin",
            "obj",
            "temp",
            "logs",
            "cache",
            "backup"
        };

        ExcludedFiles = new List<string>
        {
            "*.log",
            "*.tmp",
            "*.cache",
            "*.pid",
            "*.lock",
            "thumbs.db",
            ".DS_Store",
            "desktop.ini",
            "*.user",
            "*.suo"
        };

        LogFilePath = "webapp_update_log.txt";
        _logBuffer = new StringBuilder();
        EnableLogging = true;
        EnableConsoleOutput = true;
        SimulateMode = false;

        LastStatistics = new UpdateStatistics();
    }
    #endregion

    #region 公共方法
    /// <summary>
    /// 执行更新操作
    /// </summary>
    /// <returns> 是否全部成功 </returns>
    public async Task<bool> UpdateAsync()
    {
        return await Task.Run(Update);
    }

    /// <summary>
    /// 执行更新操作（同步版本）
    /// </summary>
    public bool Update()
    {
        try
        {
            LastStatistics = new UpdateStatistics();
            LastStatistics.StartTime = DateTime.Now;

            // 验证配置
            if (!ValidateConfiguration())
            {
                return false;
            }

            WriteLog("=== Webapp更新服务器开始运行 ===");
            WriteLog($"源文件夹: {SourceFolder}");
            WriteLog($"目标服务器数量: {DestinationFolders.Count}");
            WriteLog($"模拟模式: {(SimulateMode ? "是" : "否")}");
            WriteLog("=========================================");

            // 获取源文件列表
            var sourceFiles = GetSourceFiles();
            LastStatistics.TotalFiles = sourceFiles.Count;

            WriteLog($"找到 {sourceFiles.Count} 个需要处理的文件");

            // 遍历每个目标服务器
            foreach (var destination in DestinationFolders)
            {
                var serverResult = ProcessDestination(destination, sourceFiles);
                LastStatistics.ServerResults[destination] = serverResult;

                if (!serverResult.Success)
                {
                    LastStatistics.FailedServers.Add(destination);
                }
                else
                {
                    LastStatistics.SuccessfulServers.Add(destination);
                }
            }

            LastStatistics.EndTime = DateTime.Now;
            LastStatistics.Duration = LastStatistics.EndTime - LastStatistics.StartTime;

            // 输出统计信息
            WriteLog("\n=== 更新完成统计 ===");
            WriteLog($"总文件数: {LastStatistics.TotalFiles}");
            WriteLog($"成功服务器: {LastStatistics.SuccessfulServers.Count}");
            WriteLog($"失败服务器: {LastStatistics.FailedServers.Count}");
            WriteLog($"总复制文件数: {LastStatistics.TotalCopiedFiles}");
            WriteLog($"总跳过文件数: {LastStatistics.TotalSkippedFiles}");
            WriteLog($"总错误数: {LastStatistics.TotalErrors}");
            WriteLog($"总耗时: {LastStatistics.Duration.TotalSeconds:F2} 秒");

            // 保存日志
            if (EnableLogging)
            {
                SaveLogToFile();
            }

            return LastStatistics.FailedServers.Count == 0;
        }
        catch (Exception ex)
        {
            WriteLog($"更新过程中发生严重错误: {ex.Message}");
            WriteLog($"堆栈跟踪: {ex.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// 只更新指定的目标服务器
    /// </summary>
    public bool UpdateSpecificServers(List<string> servers)
    {
        var originalDestinations = new List<string>(DestinationFolders);
        DestinationFolders = servers;
        var result = Update();
        DestinationFolders = originalDestinations;
        return result;
    }

    /// <summary>
    /// 测试连接所有目标服务器
    /// </summary>
    public Dictionary<string, bool> TestConnections()
    {
        var results = new Dictionary<string, bool>();

        WriteLog("=== 测试服务器连接 ===");

        if (DestinationFolders == null) return results;
        foreach (var destination in DestinationFolders)
        {
            WriteLog($"正在测试: {destination}");
            var connected = ConnectToNetworkShare(destination, NetworkUsername, NetworkPassword);
            results[destination] = connected;
            WriteLog($"  结果: {(connected ? "成功" : "失败")}");
        }

        return results;
    }

    /// <summary>
    /// 添加排除文件夹
    /// </summary>
    public void AddExcludedFolder(string folderName)
    {
        if (ExcludedFolders != null && !ExcludedFolders.Contains(folderName))
        {
            ExcludedFolders.Add(folderName);
        }
    }

    /// <summary>
    /// 添加排除文件
    /// </summary>
    public void AddExcludedFile(string filePattern)
    {
        if (!ExcludedFiles.Contains(filePattern))
        {
            ExcludedFiles.Add(filePattern);
        }
    }

    /// <summary>
    /// 清除所有排除规则
    /// </summary>
    public void ClearExcludedRules()
    {
        ExcludedFolders.Clear();
        ExcludedFiles.Clear();
    }

    /// <summary>
    /// 从配置文件加载设置
    /// </summary>
    public void LoadConfiguration(string configFile)
    {
        // TODO: 实现从配置文件加载
        // 可以使用 JSON、XML 或 INI 格式
        WriteLog($"从配置文件加载: {configFile}");
    }

    /// <summary>
    /// 保存当前配置到文件
    /// </summary>
    public void SaveConfiguration(string configFile)
    {
        // TODO: 实现保存配置到文件
        WriteLog($"保存配置到文件: {configFile}");
    }
    #endregion

    #region 私有方法
    private bool ValidateConfiguration()
    {
        if (string.IsNullOrEmpty(SourceFolder))
        {
            WriteLog("错误：源文件夹路径未设置");
            return false;
        }

        if (!Directory.Exists(SourceFolder))
        {
            WriteLog($"错误：源文件夹不存在！路径: {SourceFolder}");
            return false;
        }

        if (DestinationFolders == null || DestinationFolders.Count == 0)
        {
            WriteLog("错误：目标文件夹列表为空");
            return false;
        }

        if (!SimulateMode && (string.IsNullOrEmpty(NetworkUsername) || string.IsNullOrEmpty(NetworkPassword)))
        {
            WriteLog("警告：用户名或密码为空，可能无法访问网络共享");
        }

        return true;
    }

    private List<FileInfo> GetSourceFiles()
    {
        var files = new List<FileInfo>();

        try
        {
            if (SourceFolder != null)
            {
                var directory = new DirectoryInfo(SourceFolder);
                var allFiles = directory.GetFiles("*", SearchOption.AllDirectories);

                files.AddRange(allFiles.Where(file => !IsExcluded(file.FullName)));
            }
        }
        catch (Exception ex)
        {
            WriteLog($"获取源文件列表失败: {ex.Message}");
        }

        return files;
    }

    private ServerProcessResult ProcessDestination(string destination, List<FileInfo> sourceFiles)
    {
        var result = new ServerProcessResult
        {
            Destination = destination,
            StartTime = DateTime.Now
        };

        WriteLog($"\n开始处理目标服务器: {destination}");

        try
        {
            // 连接网络共享
            if (!SimulateMode)
            {
                if (!ConnectToNetworkShare(destination, NetworkUsername, NetworkPassword))
                {
                    WriteLog($"  错误：无法连接到网络共享 {destination}");
                    result.Success = false;
                    result.ErrorMessage = "连接失败";
                    return result;
                }
            }
            else
            {
                WriteLog("  [模拟模式] 跳过连接步骤");
            }

            // 创建目标根目录
            if (!SimulateMode)
            {
                EnsureDirectoryExists(destination);
            }

            // 复制文件
            var copiedCount = 0;
            var skippedCount = 0;
            var errorCount = 0;

            foreach (var sourceFile in sourceFiles)
            {
                var relativePath = GetRelativePath(SourceFolder, sourceFile.FullName);
                var destFile = Path.Combine(destination, relativePath);

                if (IsExcluded(sourceFile.FullName))
                {
                    skippedCount++;
                    continue;
                }

                if (SimulateMode)
                {
                    WriteLog($"  [模拟模式] 将会复制: {relativePath}");
                    copiedCount++;
                    continue;
                }

                try
                {
                    // 确保目标目录存在
                    var destDir = Path.GetDirectoryName(destFile);
                    if(string.IsNullOrEmpty(destDir)) throw new FileNotFoundException();
                    EnsureDirectoryExists(destDir);

                    // 检查是否需要更新
                    if (File.Exists(destFile))
                    {
                        var destFileInfo = new FileInfo(destFile);
                        if (destFileInfo.LastWriteTime >= sourceFile.LastWriteTime &&
                            destFileInfo.Length == sourceFile.Length)
                        {
                            skippedCount++;
                            continue;
                        }
                    }

                    // 复制文件
                    File.Copy(sourceFile.FullName, destFile, true);
                    copiedCount++;

                    if (copiedCount % 50 == 0)
                    {
                        WriteLog($"  进度: {copiedCount}/{sourceFiles.Count} 文件已复制");
                    }

                    result.CopiedFiles?.Add(relativePath);
                }
                catch (Exception ex)
                {
                    errorCount++;
                    WriteLog($"  错误：复制文件 {relativePath} 失败 - {ex.Message}");
                    result.FailedFiles?.Add(new FailedFileInfo
                    {
                        FilePath = relativePath,
                        ErrorMessage = ex.Message
                    });
                }
            }

            result.CopiedCount = copiedCount;
            result.SkippedCount = skippedCount;
            result.ErrorCount = errorCount;
            result.Success = errorCount == 0;
            result.EndTime = DateTime.Now;

            WriteLog($"  目标服务器处理完成: 成功 {copiedCount} 文件, 跳过 {skippedCount} 文件, 失败 {errorCount} 文件");

            // 更新总统计
            LastStatistics?.TotalCopiedFiles += copiedCount;
            LastStatistics?.TotalSkippedFiles += skippedCount;
            LastStatistics?.TotalErrors += errorCount;
        }
        catch (Exception ex)
        {
            WriteLog($"  处理目标服务器时发生错误: {ex.Message}");
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.EndTime = DateTime.Now;
            if (LastStatistics != null) LastStatistics.TotalErrors++;
        }

        return result;
    }

    private bool ConnectToNetworkShare(string networkPath, string? username, string? password)
    {
        try
        {
            if (SimulateMode)
            {
                return true;
            }

            var uri = new Uri(networkPath);
            var shareRoot = uri.GetLeftPart(UriPartial.Authority);

            var netResource = new NETRESOURCE
            {
                dwType = RESOURCETYPE_DISK,
                lpRemoteName = shareRoot
            };

            var result = WNetUseConnection(
                IntPtr.Zero,
                netResource,
                password,
                username,
                CONNECT_INTERACTIVE | CONNECT_PROMPT | CONNECT_UPDATE_PROFILE | CONNECT_CMD_SAVECRED,
                null, null, null);

            return result == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    private bool IsExcluded(string path)
    {
        // 检查排除文件夹
        foreach (var folder in ExcludedFolders)
        {
            if (path.Contains($"\\{folder}\\") ||
                path.EndsWith($"\\{folder}") ||
                path.Contains($"/{folder}/") ||
                path.EndsWith($"/{folder}"))
            {
                return true;
            }
        }

        // 检查排除文件
        var fileName = Path.GetFileName(path);
        foreach (var pattern in ExcludedFiles)
        {
            if (pattern.Contains("*"))
            {
                var extension = Path.GetExtension(fileName);
                var patternExt = pattern.Replace("*", "");
                if (!string.IsNullOrEmpty(patternExt) &&
                    extension.Equals(patternExt, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            else
            {
                if (fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private string GetRelativePath(string relativeTo, string path)
    {
        if (!relativeTo.EndsWith(Path.DirectorySeparatorChar.ToString()))
            relativeTo += Path.DirectorySeparatorChar;

        var baseUri = new Uri(relativeTo);
        var fullUri = new Uri(path);

        return Uri.UnescapeDataString(baseUri.MakeRelativeUri(fullUri).ToString())
            .Replace('/', Path.DirectorySeparatorChar);
    }

    private void WriteLog(string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var logEntry = $"{timestamp} - {message}";

        _logBuffer?.AppendLine(logEntry);

        if (EnableConsoleOutput)
        {
            Console.WriteLine(message);
        }
    }

    private void SaveLogToFile()
    {
        try
        {
            if(string.IsNullOrEmpty(LogFilePath))
                return;
            
            File.AppendAllText(LogFilePath, _logBuffer?.ToString(), Encoding.UTF8);
            _logBuffer?.Clear();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"保存日志文件失败: {ex.Message}");
        }
    }
    #endregion
}

#region 辅助类
/// <summary>
/// 更新统计信息
/// </summary>
public class UpdateStatistics
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public int TotalFiles { get; set; }
    public int TotalCopiedFiles { get; set; }
    public int TotalSkippedFiles { get; set; }
    public int TotalErrors { get; set; }
    public List<string> SuccessfulServers { get; set; } = new();
    public List<string> FailedServers { get; set; } = new();
    public Dictionary<string, ServerProcessResult> ServerResults { get; set; } = new();
}

/// <summary>
/// 服务器处理结果
/// </summary>
public class ServerProcessResult
{
    public string? Destination { get; set; }
    public bool Success { get; set; } = true;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int CopiedCount { get; set; }
    public int SkippedCount { get; set; }
    public int ErrorCount { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string>? CopiedFiles { get; set; } = [];
    public List<FailedFileInfo>? FailedFiles { get; set; } = [];
}

/// <summary>
/// 失败文件信息
/// </summary>
public class FailedFileInfo
{
    public string? FilePath { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime FailedTime { get; set; } = DateTime.Now;
}
#endregion
