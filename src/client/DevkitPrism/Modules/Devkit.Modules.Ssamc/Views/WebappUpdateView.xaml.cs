using System.Windows.Controls;
using Devkit.Core.UI.Attributes;
using Ssamc.ViewModels;

namespace Ssamc.Views;

/// <summary>
/// WebUpdateView.xaml 的交互逻辑
/// </summary>
[MenuItem(Id = "ssamc.webappupdate", ParentId = "ssamc", Title = "前端更新", Order = 21, ViewName = nameof(WebappUpdateView))]
public partial class WebappUpdateView : UserControl
{
    public WebappUpdateView(WebappUpdateViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
