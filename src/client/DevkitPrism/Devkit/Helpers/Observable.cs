using System.ComponentModel;
using System.Runtime.CompilerServices;
using Syncfusion.Windows.Shared;

namespace Devkit.Helpers;

public class Observable : NotificationObject, INotifyPropertyChanged
{
    #region INotifyPropertyChanged Members
    public event PropertyChangedEventHandler PropertyChanged;
    #endregion

    protected void Set<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
    {
        if (Equals(storage, value))
        {
            return;
        }

        storage = value;
        OnPropertyChanged(propertyName);
    }

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
