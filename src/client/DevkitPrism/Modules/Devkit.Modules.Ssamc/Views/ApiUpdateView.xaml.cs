using Module.Ssamc.ViewModels;
using System.Windows;
using System.Windows.Controls;

using Devkit.Core.UI.Attributes;

namespace Module.Ssamc.Views;

/// <summary>
/// Interaction logic for ViewA.xaml
/// </summary>
[MenuItem(Id = "modules.ssamc.api-update", ParentId = "ssamc", Title = "api update", Order = 20, ViewName = nameof(ApiUpdateView))]
public partial class ApiUpdateView : UserControl
{
    public ApiUpdateView()
    {
        InitializeComponent();
    }
}
