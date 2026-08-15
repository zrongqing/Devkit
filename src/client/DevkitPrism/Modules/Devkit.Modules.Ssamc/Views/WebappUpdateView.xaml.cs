using System.Windows.Controls;

using Devkit.Core.UI.Attributes;

namespace Ssamc.Views;

/// <summary>
/// WebUpdateView.xaml 的交互逻辑
/// </summary>
[MenuItem(Id = "modules.ssamc.webappupdate", ParentId = "ssamc", Title = "前端更新", Order = 21, ViewName = nameof(WebappUpdateView))]
public partial class WebappUpdateView : UserControl
{
    public WebappUpdateView()
    {
        InitializeComponent();
    }
}
