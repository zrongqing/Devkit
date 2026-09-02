using System.Windows.Controls;
using Devkit.Core.UI.Attributes;
using Barcode2.ViewModels;

namespace Barcode2.Views;

/// <summary>
/// WebUpdateView.xaml 的交互逻辑
/// </summary>
[MenuItem(Id = "barcode2.webappupdate", ParentId = "barcode2", Title = "前端更新", Order = 21, ViewName = nameof(WebappUpdateView))]
public partial class WebappUpdateView : UserControl
{
    public WebappUpdateView(WebappUpdateViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
