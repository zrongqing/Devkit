using Devkit.Core.UI.Attributes;
using Devkit.Core.UI.Views;

namespace Ssamc.Views;

/// <summary>
/// Interaction logic for ViewA.xaml
/// </summary>
[MenuItem(Id = "modules.ssamc.api-code-update", ParentId = "ssamc", Title = "api code update", Order = 20, ViewName = nameof(ApiUpdateView))]
public partial class ApiUpdateView : LoadingView
{
    public ApiUpdateView()
    {
        InitializeComponent();
    }
}
