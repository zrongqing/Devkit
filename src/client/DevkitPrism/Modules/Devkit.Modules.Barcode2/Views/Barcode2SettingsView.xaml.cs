using Devkit.Core.UI.Attributes;
using Devkit.Core.UI.Views;
using Barcode2.ViewModels;

namespace Barcode2.Views;

[MenuItem(
    Id = "barcode2.settings",
    ParentId = "barcode2",
    Title = "配置",
    Order = 10,
    ViewName = nameof(Barcode2SettingsView))]
public partial class Barcode2SettingsView : LoadingView
{
    public Barcode2SettingsView(Barcode2SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
