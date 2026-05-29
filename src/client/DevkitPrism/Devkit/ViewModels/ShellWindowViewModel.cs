namespace Devkit.ViewModels;

public class ShellWindowViewModel : BindableBase
{
    private string _title = "Prism Application";
    public string Title
    {
        get =>
            _title;
        set =>
            SetProperty(ref _title, value);
    }
}
