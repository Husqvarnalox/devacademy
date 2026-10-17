using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace DevAcademy.Desktop.Views;

/// <summary>One-line text prompt. Closes with the entered text, or null when cancelled.</summary>
public sealed class PromptDialog : Window
{
    public PromptDialog(string title, string message, string initial, string confirmText, string cancelText)
    {
        Title = title;
        Width = 460;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var box = new TextBox { Text = initial };
        box.AttachedToVisualTree += (_, _) =>
        {
            box.Focus();
            // Select the base name so renaming "main.py" starts with "main" selected.
            var dot = initial.LastIndexOf('.');
            box.SelectionStart = 0;
            box.SelectionEnd = dot > 0 ? dot : initial.Length;
        };
        var ok = new Button { Content = confirmText, MinWidth = 96 };
        ok.Classes.Add("primary");
        ok.Click += (_, _) => Close(box.Text);
        var cancel = new Button { Content = cancelText, MinWidth = 96 };
        cancel.Click += (_, _) => Close(null);
        Content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = title, Classes = { "h2" }, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                box,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { cancel, ok } },
            },
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close(null);
            else if (e.Key == Key.Enter) Close(box.Text);
        };
    }
}

/// <summary>A message with several buttons (for example Save / Don't save / Cancel). Closes with the button index, or -1.</summary>
public sealed class ChoiceDialog : Window
{
    public ChoiceDialog(string title, string message, IReadOnlyList<string> options)
    {
        Title = title;
        Width = 520;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        for (var i = 0; i < options.Count; i++)
        {
            var index = i;
            var b = new Button { Content = options[i], MinWidth = 96 };
            if (i == 0) b.Classes.Add("primary");
            b.Click += (_, _) => Close(index);
            buttons.Children.Add(b);
        }
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
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(-1); };
    }
}
