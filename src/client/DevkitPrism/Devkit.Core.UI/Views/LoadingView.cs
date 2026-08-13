using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;

namespace Devkit.Core.UI.Views;

/// <summary>
/// Base view for pages whose data context exposes the loading state provided by
/// <see cref="Devkit.Core.UI.Mvvm.LoadingViewModelBase"/>.
/// </summary>
[ContentProperty(nameof(ViewContent))]
public class LoadingView : UserControl
{
    public static readonly DependencyProperty ViewContentProperty = DependencyProperty.Register(
        nameof(ViewContent),
        typeof(object),
        typeof(LoadingView),
        new PropertyMetadata(null));

    public static readonly DependencyProperty LoadingMessageProperty = DependencyProperty.Register(
        nameof(LoadingMessage),
        typeof(string),
        typeof(LoadingView),
        new PropertyMetadata("正在处理…"));

    public static readonly DependencyProperty LoadingIndicatorSizeProperty = DependencyProperty.Register(
        nameof(LoadingIndicatorSize),
        typeof(double),
        typeof(LoadingView),
        new PropertyMetadata(44d));

    public LoadingView()
    {
        var contentPresenter = new ContentPresenter
        {
            Style = CreateContentStyle()
        };
        contentPresenter.SetBinding(
            ContentPresenter.ContentProperty,
            new Binding(nameof(ViewContent)) { Source = this });

        var loadingOverlay = new LoadingOverlay();
        loadingOverlay.SetBinding(
            LoadingOverlay.IsActiveProperty,
            new Binding("PageLoading.IsVisible"));
        loadingOverlay.SetBinding(
            LoadingOverlay.MessageProperty,
            new Binding(nameof(LoadingMessage)) { Source = this });
        loadingOverlay.SetBinding(
            LoadingOverlay.IndicatorSizeProperty,
            new Binding(nameof(LoadingIndicatorSize)) { Source = this });
        Panel.SetZIndex(loadingOverlay, 10000);

        var root = new Grid();
        root.Children.Add(contentPresenter);
        root.Children.Add(loadingOverlay);
        Content = root;
    }

    public object? ViewContent
    {
        get => GetValue(ViewContentProperty);
        set => SetValue(ViewContentProperty, value);
    }

    public string LoadingMessage
    {
        get => (string)GetValue(LoadingMessageProperty);
        set => SetValue(LoadingMessageProperty, value);
    }

    public double LoadingIndicatorSize
    {
        get => (double)GetValue(LoadingIndicatorSizeProperty);
        set => SetValue(LoadingIndicatorSizeProperty, value);
    }

    private static Style CreateContentStyle()
    {
        var style = new Style(typeof(ContentPresenter));
        var busyTrigger = new DataTrigger
        {
            Binding = new Binding("PageLoading.IsBusy"),
            Value = true
        };
        busyTrigger.Setters.Add(new Setter(IsEnabledProperty, false));
        style.Triggers.Add(busyTrigger);
        return style;
    }
}
