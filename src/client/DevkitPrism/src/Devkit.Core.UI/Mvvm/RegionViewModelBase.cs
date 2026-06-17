namespace Devkit.Core.UI.Mvvm;

public class RegionViewModelBase : ViewModelBase, INavigationAware, IConfirmNavigationRequest
{
    public RegionViewModelBase(IRegionManager regionManager)
    {
        RegionManager = regionManager;
    }
    protected IRegionManager RegionManager { get; private set; }

    #region IConfirmNavigationRequest Members
    public virtual void ConfirmNavigationRequest(NavigationContext navigationContext, Action<bool> continuationCallback)
    {
        continuationCallback(true);
    }
    #endregion

    #region INavigationAware Members
    public virtual bool IsNavigationTarget(NavigationContext navigationContext)
    {
        return true;
    }

    public virtual void OnNavigatedFrom(NavigationContext navigationContext)
    {
    }

    public virtual void OnNavigatedTo(NavigationContext navigationContext)
    {
    }
    #endregion
}
