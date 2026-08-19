using System.Windows.Controls;
using Devkit.Modules.ModuleManagement.ViewModels;

namespace Devkit.Modules.ModuleManagement.Views;

public partial class ModuleManagementView : UserControl
{
    public ModuleManagementView(ModuleManagementViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
