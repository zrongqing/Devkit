using System.Globalization;
using System.Windows.Data;

namespace Devkit.Core.UI.ValueConverters;

/// <summary>
/// 节点类型转前景色
/// </summary>
public class NodeTypeToForegroundConverter : IValueConverter
{
    #region IValueConverter Members
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is FileNodeType type)
        {
            return type switch
            {
                FileNodeType.Error => "#FFE81123",
                FileNodeType.Drive => "#FF0078D7",
                _                  => "#FF000000"
            };
        }
        return "#FF000000";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
    #endregion
}
