using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DevkitCore.Models;

public class TreeNodeModel : INotifyPropertyChanged
{
    private string _name;
    private bool _isChecked;
    private bool _isExpanded;
    private ObservableCollection<TreeNodeModel> _children;

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

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
