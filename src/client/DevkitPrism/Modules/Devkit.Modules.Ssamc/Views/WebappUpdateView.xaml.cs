using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using Devkit.Core.UI.Attributes;

namespace Module.Ssamc.Views;

/// <summary>
/// WebUpdateView.xaml 的交互逻辑
/// </summary>
[MenuItem(Id = "modules.ssamc.webapp-update", ParentId = "ssamc", Title = "webapp update", Order = 21, ViewName = nameof(WebappUpdateView))]
public partial class WebappUpdateView : UserControl
{
    public WebappUpdateView()
    {
        InitializeComponent();

        //HandyControl.Controls.Dialog.Register("WebappUpdateView", this);
    }
}
