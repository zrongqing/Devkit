using System.Windows;

public static class ViewModelLocator
{
    public static string GetViewModelName(DependencyObject obj)
    {
        return (string)obj.GetValue(ViewModelNameProperty);
    }

    public static void SetViewModelName(DependencyObject obj, string value)
    {
        obj.SetValue(ViewModelNameProperty, value);
    }

    public static readonly DependencyProperty ViewModelNameProperty =
        DependencyProperty.RegisterAttached("ViewModelName", typeof(string), typeof(ViewModelLocator),
        new PropertyMetadata(null, OnViewModelNameChanged));

    private static void OnViewModelNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement view)
        {
            // 从全局 DI 容器中解析 ViewModel
            //var vmType = App.ServiceProvider.GetService(Type.GetType(e.NewValue.ToString()));
            //view.DataContext = vmType;
        }
    }
}