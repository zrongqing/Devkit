using System.Windows.Controls;
using Devkit.Core.UI.Attributes;
using Devkit.Modules.ModuleName.ViewModels;

namespace Devkit.Modules.ModuleName.Views;

/// <summary>
/// Interaction logic for ViewA.xaml
/// </summary>
[MenuItem(Id = "modules.viewA", ParentId = "modules", Title = "示例页面", Order = 10, ViewName = "ViewA")]
public partial class ViewA : UserControl
{
    public ViewA(ViewAViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
