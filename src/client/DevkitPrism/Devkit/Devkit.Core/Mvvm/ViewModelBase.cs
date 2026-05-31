using CommunityToolkit.Mvvm.ComponentModel;

namespace Devkit.Core.Mvvm;

public abstract class ViewModelBase : ObservableObject, IDestructible
{
    protected ViewModelBase()
    {

    }

    public virtual void Destroy()
    {

    }
}