using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Devkit.Core.UI.Views;

public partial class LoadingOverlay : UserControl
{
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive),
        typeof(bool),
        typeof(LoadingOverlay),
        new PropertyMetadata(false));

    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message),
        typeof(string),
        typeof(LoadingOverlay),
        new PropertyMetadata("加载中…"));

    public static readonly DependencyProperty IndicatorSizeProperty = DependencyProperty.Register(
        nameof(IndicatorSize),
        typeof(double),
        typeof(LoadingOverlay),
        new PropertyMetadata(52d));

    public static readonly DependencyProperty OverlayBrushProperty = DependencyProperty.Register(
        nameof(OverlayBrush),
        typeof(Brush),
        typeof(LoadingOverlay),
        new PropertyMetadata(new SolidColorBrush(Color.FromArgb(112, 15, 23, 42))));

    public LoadingOverlay()
    {
        InitializeComponent();
    }

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public double IndicatorSize
    {
        get => (double)GetValue(IndicatorSizeProperty);
        set => SetValue(IndicatorSizeProperty, value);
    }

    public Brush OverlayBrush
    {
        get => (Brush)GetValue(OverlayBrushProperty);
        set => SetValue(OverlayBrushProperty, value);
    }
}
