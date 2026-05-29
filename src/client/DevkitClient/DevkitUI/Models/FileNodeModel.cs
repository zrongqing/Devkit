//using System.Collections.ObjectModel;
//using System.ComponentModel;
//using System.Runtime.CompilerServices;

//namespace De
//vkitCore.Models;

//public enum FileNodeType
//{
//    Drive,
//    Folder,
//    File,
//    Error,
//    Loading
//}

//public class FileNodeModel : INotifyPropertyChanged
//{
//    private string _name;
//    private string _fullPath;
//    private bool? _isChecked = false;
//    private bool _isExpanded;
//    private bool _isSelected;
//    private string _icon;
//    private ObservableCollection<FileNodeModel> _children;
//    private bool _isLoading;
//    private long _fileSize;
//    private DateTime _modifiedTime;
//    private FileNodeModel _parent;
//    private bool _isUpdatingCheckedState = false;   // 添加一个标志位防止递归循环
//    /// <summary>
//    /// 文件名字
//    /// </summary>
//    public string Name
//    {
//        get => _name;
//        set
//        {
//            _name = value;
//            OnPropertyChanged();
//        }
//    }
//    /// <summary>
//    /// 文件完整路径
//    /// </summary>
//    public string FullPath
//    {
//        get => _fullPath;
//        set
//        {
//            _fullPath = value;
//            OnPropertyChanged();
//        }
//    }
//    /// <summary>
//    /// 勾选状态：true=全选，false=全不选，null=部分选
//    /// </summary>
//    public bool? IsChecked
//    {
//        get => _isChecked;
//        set
//        {
//            if (_isChecked != value)
//            {
//                // 防止递归循环
//                if (_isUpdatingCheckedState) return;

//                _isUpdatingCheckedState = true;

//                _isChecked = value;
//                OnPropertyChanged();

//                // 只有确定状态（true/false）时才同步所有子节点
//                if (value.HasValue)
//                {
//                    // 递归设置所有层级的子节点
//                    SetChildrenCheckedRecursive(value.Value);
//                }

//                _isUpdatingCheckedState = false;

//                // 更新所有层级的父节点状态（递归向上）
//                UpdateParentCheckedStateRecursive();
//            }
//        }
//    }
//    /// <summary>
//    /// 展开
//    /// </summary>
//    public bool IsExpanded
//    {
//        get => _isExpanded;
//        set
//        {
//            if (_isExpanded != value)
//            {
//                _isExpanded = value;
//                OnPropertyChanged();

//                // 展开时异步加载子节点，并同步勾选状态
//                if (value && ShouldLoadChildren())
//                {
//                    LoadChildrenAsync();
//                }
//            }
//        }
//    }
//    /// <summary>
//    /// 选中
//    /// </summary>
//    public bool IsSelected
//    {
//        get => _isSelected;
//        set
//        {
//            _isSelected = value;
//            OnPropertyChanged();
//        }
//    }
//    /// <summary>
//    /// 文件类型
//    /// </summary>
//    public FileNodeType NodeType { get; set; }

//    public string Icon
//    {
//        get => _icon;
//        set
//        {
//            _icon = value;
//            OnPropertyChanged();
//        }
//    }

//    public ObservableCollection<FileNodeModel> Children
//    {
//        get => _children;
//        set
//        {
//            _children = value;
//            OnPropertyChanged();
//        }
//    }

//    public bool IsLoading
//    {
//        get => _isLoading;
//        set
//        {
//            _isLoading = value;
//            OnPropertyChanged();
//        }
//    }

//    public long FileSize
//    {
//        get => _fileSize;
//        set
//        {
//            _fileSize = value;
//            OnPropertyChanged();
//            OnPropertyChanged(nameof(FileSizeDisplay));
//        }
//    }

//    public DateTime ModifiedTime
//    {
//        get => _modifiedTime;
//        set
//        {
//            _modifiedTime = value;
//            OnPropertyChanged();
//        }
//    }

//    public FileNodeModel Parent
//    {
//        get => _parent;
//        set
//        {
//            _parent = value;
//            OnPropertyChanged();
//        }
//    }

//    public string FileSizeDisplay
//    {
//        get
//        {
//            if (FileSize == 0) return "0 KB";

//            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
//            double len = FileSize;
//            int order = 0;
//            while (len >= 1024 && order < sizes.Length - 1)
//            {
//                order++;
//                len = len / 1024;
//            }
//            return $"{len:0.##} {sizes[order]}";
//        }
//    }

//    public event PropertyChangedEventHandler PropertyChanged;

//    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
//    {
//        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
//    }

//    private bool ShouldLoadChildren()
//    {
//        return Children != null &&
//               Children.Count == 1 &&
//               Children[0].NodeType == FileNodeType.Loading;
//    }

//    /// <summary>
//    /// 递归设置所有层级的子节点
//    /// </summary>
//    internal void SetChildrenCheckedRecursive(bool value)
//    {
//        if (Children == null) return;

//        foreach (var child in Children)
//        {
//            child.SetCheckedSilently(value);
//            child.SetChildrenCheckedRecursive(value);
//        }
//    }

//    private async void LoadChildrenAsync()
//    {
//        IsLoading = true;

//        try
//        {
//            var realChildren = await System.Threading.Tasks.Task.Run(() =>
//                FileSystemHelper.GetChildNodes(FullPath));

//            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
//            {
//                // 保存当前的勾选状态
//                bool? currentCheckedState = _isChecked;

//                // 清空并添加真实子节点
//                Children.Clear();

//                foreach (var child in realChildren)
//                {
//                    // 设置父节点引用
//                    child.Parent = this;

//                    // 如果父节点已勾选，子节点自动勾选（递归）
//                    if (currentCheckedState == true)
//                    {
//                        child.SetCheckedSilently(true);
//                        child.SetChildrenCheckedRecursive(true);
//                    }
//                    else if (currentCheckedState == false)
//                    {
//                        child.SetCheckedSilently(false);
//                        child.SetChildrenCheckedRecursive(false);
//                    }

//                    Children.Add(child);
//                }

//                // 如果父节点是部分选中状态，重新计算所有子节点状态
//                if (currentCheckedState == null)
//                {
//                    SyncCheckedStateFromChildren();
//                }

//                // 确保父节点状态正确
//                UpdateParentCheckedStateRecursive();
//            });
//        }
//        catch (UnauthorizedAccessException)
//        {
//            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
//            {
//                Children.Clear();
//                var errorNode = FileSystemHelper.CreateErrorNode("无访问权限");
//                errorNode.Parent = this;
//                Children.Add(errorNode);
//            });
//        }
//        catch (System.Exception ex)
//        {
//            System.Diagnostics.Debug.WriteLine($"加载子节点失败: {ex.Message}");
//        }
//        finally
//        {
//            IsLoading = false;
//        }
//    }

//    /// <summary>
//    /// 递归更新所有层级的父节点状态
//    /// </summary>
//    private void UpdateParentCheckedStateRecursive()
//    {
//        if (Parent == null) return;

//        // 防止递归循环
//        if (Parent._isUpdatingCheckedState) return;

//        Parent._isUpdatingCheckedState = true;

//        // 更新直接父节点状态
//        Parent.SyncCheckedStateFromChildren();

//        Parent._isUpdatingCheckedState = false;

//        // 递归更新更上层的父节点
//        Parent.UpdateParentCheckedStateRecursive();
//    }

//    /// <summary>
//    /// 静默设置勾选状态，不触发子节点同步
//    /// </summary>
//    internal void SetCheckedSilently(bool? value)
//    {
//        if (_isChecked != value)
//        {
//            _isChecked = value;
//            OnPropertyChanged(nameof(IsChecked));
//        }
//    }

//    /// <summary>
//    /// 根据子节点状态重新计算自身状态
//    /// </summary>
//    public void SyncCheckedStateFromChildren()
//    {
//        if (Children == null || Children.Count == 0) return;

//        bool allChecked = true;
//        bool allUnchecked = true;

//        foreach (var child in Children)
//        {
//            if (child.IsChecked == true)
//            {
//                allUnchecked = false;
//            }
//            else if (child.IsChecked == false)
//            {
//                allChecked = false;
//            }
//            else if (child.IsChecked == null)
//            {
//                allChecked = false;
//                allUnchecked = false;
//            }
//        }

//        if (allChecked)
//        {
//            SetCheckedSilently(true);
//        }
//        else if (allUnchecked)
//        {
//            SetCheckedSilently(false);
//        }
//        else
//        {
//            SetCheckedSilently(null);
//        }
//    }

//    /// <summary>
//    /// 递归获取所有选中的文件
//    /// </summary>
//    public List<FileNodeModel> GetCheckedFiles()
//    {
//        var result = new List<FileNodeModel>();

//        if (NodeType == FileNodeType.File && IsChecked == true)
//        {
//            result.Add(this);
//        }

//        if (Children != null)
//        {
//            foreach (var child in Children)
//            {
//                result.AddRange(child.GetCheckedFiles());
//            }
//        }

//        return result;
//    }

//    /// <summary>
//    /// 递归获取所有选中的节点
//    /// </summary>
//    public List<FileNodeModel> GetCheckedNodes()
//    {
//        var result = new List<FileNodeModel>();

//        if (IsChecked == true)
//        {
//            result.Add(this);
//        }

//        if (Children != null)
//        {
//            foreach (var child in Children)
//            {
//                result.AddRange(child.GetCheckedNodes());
//            }
//        }

//        return result;
//    }
//}
