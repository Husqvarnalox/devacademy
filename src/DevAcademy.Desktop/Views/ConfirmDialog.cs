using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace DevAcademy.Desktop.Views;

/// <summary>Small modal confirmation window built in code (no XAML needed).</summary>
public sealed class ConfirmDialog : Window
{
    public ConfirmDialog(string title, string message, string confirmText, string? cancelText, bool showCancel)
    {
        Title = title;
        Width = 460;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var confirm = new Button { Content = confirmText, MinWidth = 96 };
        confirm.Classes.Add("primary");
        confirm.Click += (_, _) => Close(true);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        if (showCancel)
        {
            var cancel = new Button { Content = cancelText, MinWidth = 96 };
            cancel.Click += (_, _) => Close(false);
            buttons.Children.Add(cancel);
        }
        buttons.Children.Add(confirm);

        Content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = title, Classes = { "h2" }, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                buttons,
            },
        };
        KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Escape) Close(false); };
    }
}
