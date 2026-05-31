using System.Collections.ObjectModel;
using System.IO;
using Devkit.Core.UI.Models;

namespace Devkit.Core.Helpers;

public static class FileSystemHelper
{
    // Segoe MDL2 Assets 图标代码
    private const string ICON_DRIVE = "\xEDA2";       // 硬盘图标
    private const string ICON_FOLDER = "\xED25";      // 文件夹图标
    private const string ICON_FOLDER_OPEN = "\xED26"; // 打开文件夹
    private const string ICON_FILE = "\xE996";        // 文件图标
    private const string ICON_IMAGE = "\xEB9F";       // 图片图标
    private const string ICON_VIDEO = "\xE116";       // 视频图标
    private const string ICON_MUSIC = "\xEC4F";       // 音乐图标
    private const string ICON_DOC = "\xE8A5";         // 文档图标
    private const string ICON_ZIP = "\xF012";         // 压缩包图标
    private const string ICON_ERROR = "\xE7BA";       // 错误图标
    private const string ICON_LOADING = "\xE895";     // 加载图标

    #region 获取根驱动器
    /// <summary>
    /// 获取所有根驱动器
    /// </summary>
    public static ObservableCollection<FileNodeModel> GetRootDrives()
    {
        var drives = new ObservableCollection<FileNodeModel>();

        try
        {
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                var driveNode = CreateDriveNode(drive);
                drives.Add(driveNode);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"获取驱动器失败: {ex.Message}");
        }

        return drives;
    }
    private static FileNodeModel CreateDriveNode(DriveInfo drive)
    {
        var driveNode = new FileNodeModel
        {
            Name = string.IsNullOrEmpty(drive.VolumeLabel)
                       ? $"{drive.Name.TrimEnd('\\')}"
                       : $"{drive.VolumeLabel} ({drive.Name.TrimEnd('\\')})",
            FullPath = drive.RootDirectory.FullName,
            NodeType = FileNodeType.Drive,
            Icon = GetDriveIcon(drive),
            IsExpanded = false
        };

        // 添加占位子节点，用于显示展开箭头
        driveNode.Children = new ObservableCollection<FileNodeModel>
        {
            CreatePlaceholderNode()
        };

        return driveNode;
    }
    private static string GetDriveIcon(DriveInfo drive)
    {
        return drive.DriveType switch
        {
            DriveType.Fixed     => "\xEDA2", // 本地磁盘
            DriveType.Removable => "\xE88E", // 可移动磁盘
            DriveType.CDRom     => "\xE959", // 光盘
            DriveType.Network   => "\xE8CE", // 网络驱动器
            _                   => "\xEDA2"
        };
    }
    #endregion

    #region 获取子节点

    private static IEnumerable<FileNodeModel> GetDirectories(string path)
    {
        var directoryInfo = new DirectoryInfo(path);

        if (!directoryInfo.Exists)
            yield break;

        var directories = directoryInfo.GetDirectories();

        foreach (var dir in directories)
        {
            if (ShouldSkipDirectory(dir))
                continue;

            var folderNode = CreateFolderNode(dir);
            yield return folderNode;
        }
    }

    private static bool ShouldSkipDirectory(DirectoryInfo dir)
    {
        return (dir.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden ||
               (dir.Attributes & FileAttributes.System) == FileAttributes.System ||
               dir.Name.StartsWith("$"); // 跳过系统卷标文件夹
    }

    private static FileNodeModel CreateFolderNode(DirectoryInfo dir)
    {
        var folderNode = new FileNodeModel
        {
            Name = dir.Name,
            FullPath = dir.FullName,
            NodeType = FileNodeType.Folder,
            Icon = ICON_FOLDER,
            IsExpanded = false
        };

        // 检查是否有任何子节点（文件或文件夹）
        bool hasAnyChildren = HasAnyChildren(dir);

        if (hasAnyChildren)
        {
            // 有子节点，显示展开按钮（添加占位节点）
            var placeholder = CreatePlaceholderNode();
            placeholder.Parent = folderNode;
            folderNode.Children = new ObservableCollection<FileNodeModel> { placeholder };
        }
        else
        {
            // 没有子节点，不显示展开按钮（空集合）
            folderNode.Children = new ObservableCollection<FileNodeModel>();
        }

        return folderNode;
    }

    /// <summary>
    /// 检查文件夹是否有子文件夹（用于判断是否需要展开按钮）
    /// </summary>
    private static bool HasSubFolders(DirectoryInfo dir)
    {
        try
        {
            // 检查是否有子文件夹
            return dir.GetDirectories().Any(d =>
                (d.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden &&
                (d.Attributes & FileAttributes.System) != FileAttributes.System &&
                !d.Name.StartsWith("$"));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 检查文件夹是否有任何子节点（文件或文件夹）- 用于判断是否需要显示展开按钮
    /// </summary>
    private static bool HasAnyChildren(DirectoryInfo dir)
    {
        try
        {
            // 检查是否有子文件夹或子文件
            bool hasSubDirectories = dir.GetDirectories().Any(d =>
                (d.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden &&
                (d.Attributes & FileAttributes.System) != FileAttributes.System &&
                !d.Name.StartsWith("$"));

            bool hasFiles = dir.GetFiles().Any(f =>
                (f.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden &&
                (f.Attributes & FileAttributes.System) != FileAttributes.System);

            return hasSubDirectories || hasFiles;
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<FileNodeModel> GetFiles(string path)
    {
        var directoryInfo = new DirectoryInfo(path);

        if (!directoryInfo.Exists)
            yield break;

        var files = directoryInfo.GetFiles();

        foreach (var file in files)
        {
            if ((file.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden ||
                (file.Attributes & FileAttributes.System) == FileAttributes.System)
                continue;

            var fileNode = CreateFileNode(file);
            yield return fileNode;
        }
    }

    private static FileNodeModel CreateFileNode(FileInfo file)
    {
        return new FileNodeModel
        {
            Name = file.Name,
            FullPath = file.FullName,
            NodeType = FileNodeType.File,
            Icon = GetFileIcon(file.Extension.ToLower()),
            Children = new ObservableCollection<FileNodeModel>(),
            FileSize = file.Length,
            ModifiedTime = file.LastWriteTime
        };
    }
    #endregion

    #region 根据路径查找节点
    /// <summary>
    /// 根据路径查找节点（同步方法）
    /// </summary>
    /// <param name="rootNodes">根节点集合</param>
    /// <param name="targetPath">目标路径</param>
    /// <returns>找到的节点，未找到返回null</returns>
    public static FileNodeModel FindNodeByPath(IEnumerable<FileNodeModel> rootNodes, string targetPath)
    {
        if (rootNodes == null || string.IsNullOrEmpty(targetPath))
            return null;

        // 规范化路径
        targetPath = NormalizePath(targetPath);

        foreach (var root in rootNodes)
        {
            var found = FindNodeRecursive(root, targetPath);
            if (found != null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// 根据路径查找节点（异步方法）
    /// </summary>
    public static async Task<FileNodeModel> FindNodeByPathAsync(IEnumerable<FileNodeModel> rootNodes, string targetPath)
    {
        return await Task.Run(() => FindNodeByPath(rootNodes, targetPath));
    }

    private static FileNodeModel FindNodeRecursive(FileNodeModel node, string targetPath)
    {
        // 检查当前节点
        if (!string.IsNullOrEmpty(node.FullPath) &&
            string.Equals(NormalizePath(node.FullPath), targetPath, StringComparison.OrdinalIgnoreCase))
        {
            return node;
        }

        // 如果是文件夹且未加载子节点，尝试加载
        if (node.NodeType == FileNodeType.Folder || node.NodeType == FileNodeType.Drive)
        {
            // 如果当前路径是目标路径的父路径，才需要加载子节点
            if (IsParentPath(node.FullPath, targetPath))
            {
                EnsureChildrenLoaded(node);
            }
        }

        // 递归搜索子节点
        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                var found = FindNodeRecursive(child, targetPath);
                if (found != null)
                    return found;
            }
        }

        return null;
    }

    /// <summary>
    /// 展开到指定路径
    /// </summary>
    /// <param name="rootNodes">根节点集合</param>
    /// <param name="targetPath">目标路径</param>
    /// <returns>展开路径上的最后一个节点</returns>
    public static async Task<FileNodeModel> ExpandToPathAsync(IEnumerable<FileNodeModel> rootNodes, string targetPath)
    {
        if (rootNodes == null || string.IsNullOrEmpty(targetPath))
            return null;

        targetPath = NormalizePath(targetPath);
        var pathParts = targetPath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

        if (pathParts.Length == 0)
            return null;

        // 查找根节点（驱动器）
        var rootName = pathParts[0];
        var rootNode = rootNodes.FirstOrDefault(r =>
            !string.IsNullOrEmpty(r.FullPath) &&
            r.FullPath.StartsWith(rootName, StringComparison.OrdinalIgnoreCase));

        if (rootNode == null)
            return null;

        return await ExpandNodeRecursiveAsync(rootNode, pathParts, 1);
    }

    private static async Task<FileNodeModel> ExpandNodeRecursiveAsync(FileNodeModel currentNode, string[] pathParts, int index)
    {
        if (currentNode == null)
            return null;

        // 展开当前节点
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            currentNode.IsExpanded = true;
        });

        if (index >= pathParts.Length)
            return currentNode;

        // 确保子节点已加载
        EnsureChildrenLoaded(currentNode);

        // 查找下一个路径部分
        var nextPathPart = pathParts[index];
        var nextNode = currentNode.Children?.FirstOrDefault(c =>
            !string.IsNullOrEmpty(c.Name) &&
            string.Equals(c.Name, nextPathPart, StringComparison.OrdinalIgnoreCase));

        if (nextNode == null)
        {
            // 尝试通过完整路径匹配
            var expectedPath = string.Join(Path.DirectorySeparatorChar.ToString(), pathParts.Take(index + 1));
            nextNode = currentNode.Children?.FirstOrDefault(c =>
                !string.IsNullOrEmpty(c.FullPath) &&
                string.Equals(NormalizePath(c.FullPath), NormalizePath(expectedPath), StringComparison.OrdinalIgnoreCase));
        }

        if (nextNode != null)
        {
            return await ExpandNodeRecursiveAsync(nextNode, pathParts, index + 1);
        }

        return currentNode;
    }

    /// <summary>
    /// 确保节点的子节点已加载
    /// </summary>
    public static void EnsureChildrenLoaded(FileNodeModel node)
    {
        if (node == null) return;

        // 如果是叶子节点或已加载，不需要处理
        if (node.NodeType == FileNodeType.File) return;

        // 如果是占位节点，加载真实数据
        if (node.Children != null &&
            node.Children.Count == 1 &&
            node.Children[0].NodeType == FileNodeType.Loading)
        {
            try
            {
                var realChildren = GetChildNodes(node.FullPath);

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // 保存当前勾选状态
                    bool? currentCheckedState = node.IsChecked;

                    node.Children.Clear();

                    if (realChildren.Any())
                    {
                        foreach (var child in realChildren)
                        {
                            child.Parent = node;

                            // 如果父节点已勾选，递归设置所有子节点
                            if (currentCheckedState == true)
                            {
                                child.SetCheckedSilently(true);
                                child.SetChildrenCheckedRecursive(true);
                            }
                            else if (currentCheckedState == false)
                            {
                                child.SetCheckedSilently(false);
                                child.SetChildrenCheckedRecursive(false);
                            }

                            node.Children.Add(child);
                        }

                        // 如果父节点是部分选中状态，重新计算
                        if (currentCheckedState == null)
                        {
                            node.SyncCheckedStateFromChildren();
                        }
                    }
                    else
                    {
                        // 没有子节点，设置为空集合（不显示展开按钮）
                        node.Children = new ObservableCollection<FileNodeModel>();
                    }
                });
            }
            catch (UnauthorizedAccessException)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    node.Children.Clear();
                    var errorNode = CreateErrorNode("无访问权限");
                    errorNode.Parent = node;
                    node.Children = new ObservableCollection<FileNodeModel> { errorNode };
                });
            }
        }
    }
    #endregion

    #region 获取子节点 - 递归完整版

    /// <summary>
    /// 获取指定路径的子节点（文件和文件夹）- 标准版（只获取直接子节点）
    /// </summary>
    public static List<FileNodeModel> GetChildNodes(string path)
    {
        var nodes = new List<FileNodeModel>();

        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return nodes;

        try
        {
            // 获取文件夹
            var directories = GetDirectories(path, false); // 不递归
            nodes.AddRange(directories);

            // 获取文件
            var files = GetFiles(path);
            nodes.AddRange(files);
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"获取子节点失败: {ex.Message}");
        }

        // 排序：文件夹在前，文件在后，各自按名称排序
        return nodes.OrderByDescending(n => n.NodeType == FileNodeType.Folder)
            .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 获取指定路径的完整子节点树（递归所有层级）
    /// </summary>
    public static List<FileNodeModel> GetChildNodesRecursive(string path, int maxDepth = -1)
    {
        var nodes = new List<FileNodeModel>();

        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return nodes;

        try
        {
            // 递归获取完整文件夹树
            var directories = GetDirectories(path, true, 0, maxDepth);
            nodes.AddRange(directories);

            // 获取直接文件（文件不需要递归）
            var files = GetFiles(path);
            nodes.AddRange(files);
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"获取递归子节点失败: {ex.Message}");
        }

        return nodes.OrderByDescending(n => n.NodeType == FileNodeType.Folder)
            .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 获取文件夹节点 - 支持递归
    /// </summary>
    /// <param name="path">路径</param>
    /// <param name="recursive">是否递归获取所有子文件夹</param>
    /// <param name="currentDepth">当前递归深度</param>
    /// <param name="maxDepth">最大递归深度，-1表示无限制</param>
    private static IEnumerable<FileNodeModel> GetDirectories(string path, bool recursive = false, int currentDepth = 0, int maxDepth = -1)
    {
        var directoryInfo = new DirectoryInfo(path);

        if (!directoryInfo.Exists)
            yield break;

        // 检查递归深度
        if (maxDepth != -1 && currentDepth > maxDepth)
            yield break;

        DirectoryInfo[] directories;
        try
        {
            directories = directoryInfo.GetDirectories();
        }
        catch (UnauthorizedAccessException)
        {
            yield break;
        }
        catch
        {
            yield break;
        }

        foreach (var dir in directories)
        {
            if (ShouldSkipDirectory(dir))
                continue;

            // 创建文件夹节点
            var folderNode = CreateFolderNode(dir, recursive, currentDepth, maxDepth);
            yield return folderNode;
        }
    }

    /// <summary>
    /// 创建文件夹节点 - 支持递归构建完整子树
    /// </summary>
    private static FileNodeModel CreateFolderNode(DirectoryInfo dir, bool recursive = false, int currentDepth = 0, int maxDepth = -1)
    {
        var folderNode = new FileNodeModel
        {
            Name = dir.Name,
            FullPath = dir.FullName,
            NodeType = FileNodeType.Folder,
            Icon = ICON_FOLDER,
            IsExpanded = false,
            ModifiedTime = dir.LastWriteTime
        };

        try
        {
            if (recursive)
            {
                // 递归模式：一次性加载所有子节点
                var children = new ObservableCollection<FileNodeModel>();

                // 递归获取子文件夹
                var subDirectories = GetDirectories(dir.FullName, true, currentDepth + 1, maxDepth);
                foreach (var subDir in subDirectories)
                {
                    subDir.Parent = folderNode;
                    children.Add(subDir);
                }

                // 获取当前文件夹的文件
                var files = GetFiles(dir.FullName);
                foreach (var file in files)
                {
                    file.Parent = folderNode;
                    children.Add(file);
                }

                folderNode.Children = children;
            }
            else
            {
                // 非递归模式：只检查是否有子节点，添加占位符
                bool hasAnyChildren = HasAnyChildren(dir);

                if (hasAnyChildren)
                {
                    var placeholder = CreatePlaceholderNode();
                    placeholder.Parent = folderNode;
                    folderNode.Children = new ObservableCollection<FileNodeModel> { placeholder };
                }
                else
                {
                    folderNode.Children = new ObservableCollection<FileNodeModel>();
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            folderNode.Children = new ObservableCollection<FileNodeModel>
            {
                CreateErrorNode("无访问权限")
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"创建文件夹节点失败: {ex.Message}");
            folderNode.Children = new ObservableCollection<FileNodeModel>();
        }

        return folderNode;
    }

    /// <summary>
    /// 获取指定路径的完整文件夹树（返回根节点）
    /// </summary>
    public static FileNodeModel GetFolderTree(string rootPath, int maxDepth = -1)
    {
        if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
            return null;

        try
        {
            var dirInfo = new DirectoryInfo(rootPath);
            return CreateFolderNode(dirInfo, true, 0, maxDepth);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"获取文件夹树失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 获取指定路径的所有子文件夹路径（平铺列表）
    /// </summary>
    public static List<string> GetAllSubfolderPaths(string rootPath, int maxDepth = -1)
    {
        var paths = new List<string>();

        if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
            return paths;

        try
        {
            GetAllSubfolderPathsRecursive(rootPath, paths, 0, maxDepth);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"获取子文件夹路径失败: {ex.Message}");
        }

        return paths.OrderBy(p => p).ToList();
    }

    private static void GetAllSubfolderPathsRecursive(string path, List<string> paths, int currentDepth, int maxDepth)
    {
        if (maxDepth != -1 && currentDepth > maxDepth)
            return;

        try
        {
            foreach (var dir in Directory.GetDirectories(path))
            {
                try
                {
                    var dirInfo = new DirectoryInfo(dir);
                    if (!ShouldSkipDirectory(dirInfo))
                    {
                        paths.Add(dir);
                        GetAllSubfolderPathsRecursive(dir, paths, currentDepth + 1, maxDepth);
                    }
                }
                catch
                {
                    // 跳过无法访问的目录
                }
            }
        }
        catch
        {
            // 跳过无法访问的目录
        }
    }

    /// <summary>
    /// 获取指定路径的所有子节点（文件和文件夹）的平铺列表
    /// </summary>
    public static List<FileNodeModel> GetAllChildNodesFlattened(string rootPath, int maxDepth = -1)
    {
        var results = new List<FileNodeModel>();

        if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
            return results;

        try
        {
            var rootDir = new DirectoryInfo(rootPath);

            // 添加根文件夹
            var rootNode = CreateFolderNode(rootDir, false);
            results.Add(rootNode);

            // 递归获取所有子节点
            GetAllNodesFlattenedRecursive(rootDir, results, 0, maxDepth);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"获取平铺节点失败: {ex.Message}");
            results.Add(CreateErrorNode($"错误: {ex.Message}"));
        }

        return results;
    }

    private static void GetAllNodesFlattenedRecursive(DirectoryInfo directory, List<FileNodeModel> results, int currentDepth, int maxDepth)
    {
        if (maxDepth != -1 && currentDepth >= maxDepth)
            return;

        try
        {
            // 获取所有子文件夹
            foreach (var subDir in directory.GetDirectories())
            {
                try
                {
                    if (!ShouldSkipDirectory(subDir))
                    {
                        var folderNode = CreateFolderNode(subDir, false);
                        results.Add(folderNode);

                        GetAllNodesFlattenedRecursive(subDir, results, currentDepth + 1, maxDepth);
                    }
                }
                catch
                {
                    // 跳过无法访问的子文件夹
                }
            }

            // 获取所有文件
            foreach (var file in directory.GetFiles())
            {
                try
                {
                    if (!ShouldSkipFile(file))
                    {
                        var fileNode = CreateFileNode(file);
                        results.Add(fileNode);
                    }
                }
                catch
                {
                    // 跳过无法访问的文件
                }
            }
        }
        catch
        {
            // 跳过无法访问的目录
        }
    }

    /// <summary>
    /// 是否应该跳过文件
    /// </summary>
    private static bool ShouldSkipFile(FileInfo file)
    {
        return (file.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden ||
               (file.Attributes & FileAttributes.System) == FileAttributes.System;
    }

    #endregion

    #region 路径搜索
    /// <summary>
    /// 根据关键词搜索文件/文件夹
    /// </summary>
    public static async Task<List<FileNodeModel>> SearchNodesAsync(string rootPath, string keyword, int maxResults = 100)
    {
        var results = new List<FileNodeModel>();

        if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath) || string.IsNullOrEmpty(keyword))
            return results;

        await Task.Run(() =>
        {
            try
            {
                SearchDirectoryRecursive(rootPath, keyword, results, maxResults);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"搜索失败: {ex.Message}");
            }
        });

        return results;
    }

    private static void SearchDirectoryRecursive(string directory, string keyword, List<FileNodeModel> results, int maxResults)
    {
        if (results.Count >= maxResults) return;

        try
        {
            // 搜索当前目录的文件
            var files = Directory.GetFiles(directory);
            foreach (var file in files)
            {
                if (results.Count >= maxResults) break;

                var fileName = Path.GetFileName(file);
                if (fileName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var fileInfo = new FileInfo(file);
                    results.Add(new FileNodeModel
                    {
                        Name = fileName,
                        FullPath = file,
                        NodeType = FileNodeType.File,
                        Icon = GetFileIcon(fileInfo.Extension.ToLower()),
                        ModifiedTime = fileInfo.LastWriteTime,
                        FileSize = fileInfo.Length
                    });
                }
            }

            // 递归搜索子目录
            var directories = Directory.GetDirectories(directory);
            foreach (var dir in directories)
            {
                if (results.Count >= maxResults) break;

                try
                {
                    var dirName = Path.GetFileName(dir);
                    if (dirName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        results.Add(new FileNodeModel
                        {
                            Name = dirName,
                            FullPath = dir,
                            NodeType = FileNodeType.Folder,
                            Icon = ICON_FOLDER
                        });
                    }

                    SearchDirectoryRecursive(dir, keyword, results, maxResults);
                }
                catch
                {
                    // 跳过无法访问的目录
                }
            }
        }
        catch
        {
            // 跳过无法访问的目录
        }
    }
    #endregion

    #region 辅助方法
    /// <summary>
    /// 创建占位节点
    /// </summary>
    public static FileNodeModel CreatePlaceholderNode()
    {
        return new FileNodeModel
        {
            Name = "加载中...",
            NodeType = FileNodeType.Loading,
            Icon = ICON_LOADING
        };
    }

    /// <summary>
    /// 创建错误节点
    /// </summary>
    public static FileNodeModel CreateErrorNode(string errorMessage)
    {
        return new FileNodeModel
        {
            Name = errorMessage,
            NodeType = FileNodeType.Error,
            Icon = ICON_ERROR
        };
    }

    /// <summary>
    /// 规范化路径
    /// </summary>
    public static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        try
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }

    /// <summary>
    /// 判断一个路径是否是另一个路径的父路径
    /// </summary>
    public static bool IsParentPath(string parentPath, string childPath)
    {
        if (string.IsNullOrEmpty(parentPath) || string.IsNullOrEmpty(childPath))
            return false;

        parentPath = NormalizePath(parentPath);
        childPath = NormalizePath(childPath);

        return childPath.StartsWith(parentPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 获取文件图标
    /// </summary>
    private static string GetFileIcon(string extension)
    {
        switch (extension)
        {
            case ".jpg":
            case ".jpeg":
            case ".png":
            case ".bmp":
            case ".gif":
            case ".ico":
            case ".webp":
                return ICON_IMAGE;
            case ".mp4":
            case ".avi":
            case ".mkv":
            case ".mov":
            case ".wmv":
            case ".flv":
            case ".webm":
                return ICON_VIDEO;
            case ".mp3":
            case ".wav":
            case ".flac":
            case ".m4a":
            case ".wma":
            case ".aac":
                return ICON_MUSIC;
            case ".doc":
            case ".docx":
            case ".txt":
            case ".pdf":
            case ".xls":
            case ".xlsx":
            case ".ppt":
            case ".pptx":
            case ".md":
                return ICON_DOC;
            case ".zip":
            case ".rar":
            case ".7z":
            case ".tar":
            case ".gz":
            case ".bz2":
                return ICON_ZIP;
            case ".exe":
            case ".msi":
            case ".bat":
            case ".cmd":
                return "\xE950"; // 应用程序图标
            case ".cs":
            case ".cpp":
            case ".h":
            case ".java":
            case ".py":
            case ".js":
            case ".html":
            case ".css":
                return "\xE943"; // 代码图标
            default:
                return ICON_FILE;
        }
    }
    #endregion
}