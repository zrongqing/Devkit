using Devkit.Core.UI.Attributes;
using Devkit.Core.UI.Views;

namespace Module.Ssamc.Views;

/// <summary>
/// Interaction logic for ViewA.xaml
/// </summary>
[MenuItem(Id = "modules.ssamc.api-update", ParentId = "ssamc", Title = "api update", Order = 20, ViewName = nameof(ApiUpdateView))]
public partial class ApiUpdateView : LoadingView
{
    public ApiUpdateView()
    {
        InitializeComponent();
    }
}
