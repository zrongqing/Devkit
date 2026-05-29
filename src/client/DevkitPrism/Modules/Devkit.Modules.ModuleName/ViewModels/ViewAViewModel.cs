using Devkit.Core.UI.Mvvm;
using Devkit.Services.Interfaces;

namespace Devkit.Modules.ModuleName.ViewModels;

public class ViewAViewModel : RegionViewModelBase
{
    private string _message;

    public ViewAViewModel(IRegionManager regionManager, IMessageService messageService) :
        base(regionManager)
    {
        Message = messageService.GetMessage();
    }
    public string Message
    {
        get =>
            _message;
        set =>
            SetProperty(ref _message, value);
    }

    public override void OnNavigatedTo(NavigationContext navigationContext)
    {
        //do something
    }
}
