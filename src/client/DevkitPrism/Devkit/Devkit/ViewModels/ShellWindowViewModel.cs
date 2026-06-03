using Prism.Mvvm;

namespace Devkit.ViewModels;

public class ShellWindowViewModel : BindableBase
{
    private string _title = "Prism Application";
    public string Title
    {
        get { return _title; }
        set { SetProperty(ref _title, value); }
    }

    public ShellWindowViewModel()
    {

    }
}