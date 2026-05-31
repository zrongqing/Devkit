using System.Globalization;
using System.Windows.Data;

namespace Devkit.Core.UI.ValueConverters;

// 三态布尔值转背景色（用于部分选中状态显示）
public class NullableBoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if(value is null)
        {
            return "#FF0078D7";
        }
        if (value is bool isChecked)
        {
            return isChecked switch
            {
                true => "#FF107C10",  // 绿色
                false => "#FF666666", // 灰色
            };
        }
        return "#FF666666";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
