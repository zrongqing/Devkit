using Devkit.Core.UI.Attributes;
using Devkit.Core.UI.Views;
using Barcode2.ViewModels;

namespace Barcode2.Views;

/// <summary>
/// Interaction logic for ViewA.xaml
/// </summary>
[MenuItem(Id = "barcode2.apiupdate", ParentId = "barcode2", Title = "API更新", Order = 20, ViewName = nameof(ApiUpdateView))]
public partial class ApiUpdateView : LoadingView
{
    public ApiUpdateView(ApiUpdateViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
