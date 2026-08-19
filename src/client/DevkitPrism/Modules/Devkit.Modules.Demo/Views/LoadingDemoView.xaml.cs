using Devkit.Core.UI.Attributes;
using Devkit.Core.UI.Views;
using Devkit.Modules.Demo.ViewModels;

namespace Devkit.Modules.Demo.Views;

[MenuItem(
    Id = "modules.demo.loading",
    ParentId = "demo",
    Title = "loading",
    Order = 0,
    ViewName = nameof(LoadingDemoView))]
public partial class LoadingDemoView : LoadingView
{
    public LoadingDemoView(LoadingDemoViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
