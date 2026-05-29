using CommunityToolkit.Mvvm.ComponentModel;

namespace Devkit.Core.UI.Mvvm;

public abstract class ViewModelBase : ObservableObject, IDestructible
{
    #region IDestructible Members
    public virtual void Destroy()
    {
    }
    #endregion
}
