using System.Globalization;
using System.Windows.Data;

namespace Devkit.Core.UI.ValueConverters;

/// <summary>
/// 节点类型启用复选框转换器
/// </summary>
public class NodeTypeToEnabledConverter : IValueConverter
{
    #region IValueConverter Members
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is FileNodeType type)
        {
            return type != FileNodeType.Error &&
                   type != FileNodeType.Loading &&
                   type != FileNodeType.Drive; // 可选：是否允许勾选驱动器
        }
        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
    #endregion
}
