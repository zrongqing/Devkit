using System.Windows.Controls;

using Devkit.ViewModels;

namespace Devkit.Views;

public partial class MainPage : Page
{
    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
