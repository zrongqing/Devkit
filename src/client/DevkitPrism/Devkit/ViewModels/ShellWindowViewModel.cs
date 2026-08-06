using Devkit.Core.UI.Mvvm;

namespace Devkit.ViewModels;

public class ShellWindowViewModel : BindableBase
{
    private string _title = "Devkit";

    public ShellWindowViewModel(DelayedLoadingState globalLoading)
    {
        GlobalLoading = globalLoading;
    }

    public DelayedLoadingState GlobalLoading { get; }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }
}
