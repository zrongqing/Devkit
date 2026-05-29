using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Devkit.Core.UI.Models;

public class TreeNodeModel : INotifyPropertyChanged
{
    private ObservableCollection<TreeNodeModel> _children;
    private bool _isChecked;
    private bool _isExpanded;
    private string _name;

    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            OnPropertyChanged();
        }
    }

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            _isChecked = value;
            OnPropertyChanged();

            // 子节点全选/取消全选
            if (Children != null)
            {
                foreach (var child in Children)
                {
                    child.IsChecked = value;
                }
            }
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<TreeNodeModel> Children
    {
        get => _children;
        set
        {
            _children = value;
            OnPropertyChanged();
        }
    }

    #region INotifyPropertyChanged Members
    public event PropertyChangedEventHandler PropertyChanged;
    #endregion

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
