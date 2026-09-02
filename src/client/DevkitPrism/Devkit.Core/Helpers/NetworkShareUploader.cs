using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Devkit.Core.Helpers;

/// <summary>
/// 局域网共享文件夹上传辅助类
/// 支持指定用户名/密码连接Windows共享(SMB)
/// </summary>
public class NetworkShareUploader : IDisposable
{
    private readonly NetworkShareAuthenticationMode _authenticationMode;
    private readonly string _password;

    private readonly string _shareRoot;
    private readonly string _username;
    private int _connectionDepth;
    private bool _ownsConnection;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="shareRoot"> 共享根目录，如 \\fileserver.example.invalid\SharedFolder </param>
    /// <param name="username"> 用户名，格式：机器名\用户名 或 域\用户名 </param>
    /// <param name="password"> 密码 </param>
    public NetworkShareUploader(
        string shareRoot,
        string username,
        string password,
        NetworkShareAuthenticationMode authenticationMode =
            NetworkShareAuthenticationMode.PreferCurrentWindowsIdentity)
    {
        if (string.IsNullOrWhiteSpace(shareRoot))
            throw new ArgumentException("共享路径不能为空");

        _shareRoot = shareRoot.TrimEnd('\\');
        _username = username;
        _password = password;
        _authenticationMode = authenticationMode;
    }

    /// <summary>
    /// 连接网络共享
    /// </summary>
    public void Connect()
    {
        if (_connectionDepth > 0)
        {
            _connectionDepth++;
            return;
        }

        // Prefer the current Windows identity or an existing SMB session. This avoids
        // replacing a connection that is already available to the signed-in user.
        if (_authenticationMode == NetworkShareAuthenticationMode.PreferCurrentWindowsIdentity &&
            Directory.Exists(_shareRoot))
        {
            _connectionDepth = 1;
            _ownsConnection = false;
            return;
        }

        if (string.IsNullOrWhiteSpace(_username) || string.IsNullOrWhiteSpace(_password))
        {
            throw new InvalidOperationException(
                $"当前 Windows 用户无法访问共享目录 [{_shareRoot}]，且未配置用于回退登录的用户名或密码。");
        }

        var netResource = new NetResource
        {
            lpRemoteName = _shareRoot
        };

        var result = WNetAddConnection2(netResource, _password, _username, 0);
        if (result != 0)
        {
            throw new Win32Exception((int)result,
                $"连接共享失败 [{_shareRoot}]，错误码：{result}，请检查用户名密码及网络");
        }

        _connectionDepth = 1;
        _ownsConnection = true;
    }

    /// <summary>
    /// 断开网络共享
    /// </summary>
    public void Disconnect()
    {
        if (_connectionDepth == 0)
            return;

        _connectionDepth--;
        if (_connectionDepth > 0)
            return;

        try
        {
            if (_ownsConnection)
            {
                WNetCancelConnection2(_shareRoot, 0, true);
            }
        }
        catch
        {
            // 忽略断开异常，继续释放资源
        }
        finally
        {
            _ownsConnection = false;
        }
    }

    #region Nested type: NetResource
    #region 结构定义
    [StructLayout(LayoutKind.Sequential)]
    private class NetResource
    {
        public int dwScope = 2;           // RESOURCE_GLOBALNET
        public int dwType = 1;            // RESOURCETYPE_DISK
        public int dwDisplayType = 3;     // RESOURCEDISPLAYTYPE_GENERIC
        public int dwUsage = 1;           // RESOURCEUSAGE_CONNECTABLE
        public string lpLocalName = string.Empty; // 不映射本地驱动器
        public string lpRemoteName = string.Empty;       // 共享文件夹路径（根目录）
        public string lpComment = string.Empty;
        public string lpProvider = string.Empty;
    }
    #endregion
    #endregion

    #region DLL导入
    [DllImport("mpr.dll")]
    private static extern uint WNetAddConnection2(
        NetResource lpNetResource,
        string lpPassword,
        string lpUsername,
        uint dwFlags);

    [DllImport("mpr.dll")]
    private static extern uint WNetCancelConnection2(
        string lpName,
        uint dwFlags,
        bool fForce);
    #endregion

    #region 文件操作API
    /// <summary>
    /// 上传本地文件到共享文件夹
    /// </summary>
    /// <param name="localFilePath"> 本地文件完整路径 </param>
    /// <param name="remoteFileName"> 远程文件名（含相对路径） </param>
    /// <param name="overwrite"> 是否覆盖已存在文件 </param>
    public void UploadFile(string localFilePath, string remoteFileName, bool overwrite = true)
    {
        if (!File.Exists(localFilePath))
            throw new FileNotFoundException($"本地文件不存在: {localFilePath}");

        Connect();
        try
        {
            var remotePath = CombinePath(_shareRoot, remoteFileName);

            // 确保目标目录存在
            var remoteDir = Path.GetDirectoryName(remotePath);
            if (!string.IsNullOrEmpty(remoteDir))
            {
                CreateDirectory(remoteDir);
            }

            File.Copy(localFilePath, remotePath, overwrite);
        }
        finally
        {
            Disconnect();
        }
    }

    /// <summary>
    /// 将字节数组写入共享文件
    /// </summary>
    /// <param name="bytes"> 文件内容 </param>
    /// <param name="remoteFilePath"> 远程文件路径（相对于共享根或完整UNC） </param>
    public void WriteAllBytes(byte[] bytes, string remoteFilePath)
    {
        if (bytes == null)
            throw new ArgumentNullException(nameof(bytes));

        Connect();
        try
        {
            var remotePath = CombinePath(_shareRoot, remoteFilePath);

            var remoteDir = Path.GetDirectoryName(remotePath);
            if (!string.IsNullOrEmpty(remoteDir))
            {
                CreateDirectory(remoteDir);
            }

            File.WriteAllBytes(remotePath, bytes);
        }
        finally
        {
            Disconnect();
        }
    }

    /// <summary>
    /// 将文本写入共享文件
    /// </summary>
    public void WriteAllText(string remoteFilePath, string content)
    {
        WriteAllBytes(Encoding.UTF8.GetBytes(content), remoteFilePath);
    }

    /// <summary>
    /// 从共享文件夹读取文件字节数组
    /// </summary>
    public byte[] ReadAllBytes(string remoteFilePath)
    {
        Connect();
        try
        {
            var remotePath = CombinePath(_shareRoot, remoteFilePath);
            return File.ReadAllBytes(remotePath);
        }
        finally
        {
            Disconnect();
        }
    }

    /// <summary>
    /// 检查共享文件夹中的文件/目录是否存在
    /// </summary>
    public bool Exists(string remotePath)
    {
        Connect();
        try
        {
            var fullPath = CombinePath(_shareRoot, remotePath);
            return File.Exists(fullPath) || Directory.Exists(fullPath);
        }
        finally
        {
            Disconnect();
        }
    }

    /// <summary>
    /// 删除共享文件夹中的文件
    /// </summary>
    public void DeleteFile(string remoteFilePath)
    {
        Connect();
        try
        {
            var fullPath = CombinePath(_shareRoot, remoteFilePath);
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }
        finally
        {
            Disconnect();
        }
    }

    /// <summary>
    /// 在共享文件夹中创建目录（支持多级）
    /// </summary>
    public void CreateDirectory(string remoteDirectoryPath)
    {
        Connect();
        try
        {
            //string fullPath = CombinePath(_shareRoot, remoteDirectoryPath);
            var fullPath = remoteDirectoryPath;
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
            }
        }
        finally
        {
            Disconnect();
        }
    }

    /// <summary>
    /// 获取共享文件夹中的文件列表
    /// </summary>
    public string[] GetFiles(string remoteDirectoryPath, string searchPattern = "*")
    {
        Connect();
        try
        {
            var fullPath = CombinePath(_shareRoot, remoteDirectoryPath);
            if (Directory.Exists(fullPath))
            {
                return Directory.GetFiles(fullPath, searchPattern);
            }
            return Array.Empty<string>();
        }
        finally
        {
            Disconnect();
        }
    }
    #endregion

    #region 辅助方法
    private string CombinePath(string root, string relativePath)
    {
        relativePath = relativePath.Replace('/', '\\').TrimStart('\\');
        return Path.Combine(root, relativePath);
    }

    public void Dispose()
    {
        Disconnect();
        GC.SuppressFinalize(this);
    }
    #endregion
}

public enum NetworkShareAuthenticationMode
{
    PreferCurrentWindowsIdentity,
    ConfiguredCredentials
}
