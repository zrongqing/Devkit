using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Devkit.Core.UI.Behaviors;

public static class TextBoxBehavior
{
    public static readonly DependencyProperty EnterCommandProperty =
        DependencyProperty.RegisterAttached(
            "EnterCommand",
            typeof(ICommand),
            typeof(TextBoxBehavior),
            new PropertyMetadata(null, OnEnterCommandChanged));

    private static void OnEnterCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBox textBox)
        {
            textBox.PreviewKeyDown -= TextBox_PreviewKeyDown;
            if (e.NewValue != null)
            {
                textBox.PreviewKeyDown += TextBox_PreviewKeyDown;
            }
        }
    }

    private static void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var textBox = sender as TextBox;
            var command = GetEnterCommand(textBox);
            if (command?.CanExecute(textBox.Text) == true)
            {
                command.Execute(textBox.Text);
                e.Handled = true;
            }
        }
    }

    public static void SetEnterCommand(TextBox element, ICommand value)
    {
        element.SetValue(EnterCommandProperty, value);
    }

    public static ICommand GetEnterCommand(TextBox element)
    {
        return (ICommand)element.GetValue(EnterCommandProperty);
    }
}
