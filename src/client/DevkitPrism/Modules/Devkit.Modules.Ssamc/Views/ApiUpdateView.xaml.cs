using Devkit.Core.UI.Attributes;
using Devkit.Core.UI.Views;

namespace Ssamc.Views;

/// <summary>
/// Interaction logic for ViewA.xaml
/// </summary>
[MenuItem(Id = "modules.ssamc.apiupdate", ParentId = "ssamc", Title = "API更新", Order = 20, ViewName = nameof(ApiUpdateView))]
public partial class ApiUpdateView : LoadingView
{
    public ApiUpdateView()
    {
        InitializeComponent();
    }
}
