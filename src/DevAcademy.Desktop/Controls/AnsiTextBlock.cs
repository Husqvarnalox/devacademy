using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using DevAcademy.Notebooks.Text;

namespace DevAcademy.Desktop.Controls;

/// <summary>Selectable text with ANSI colours (tracebacks, coloured stream output). Colours are mid-tone so that they read on both themes.</summary>
public sealed class AnsiTextBlock : SelectableTextBlock
{
    public static readonly StyledProperty<IReadOnlyList<AnsiRun>?> RunsProperty = AvaloniaProperty.Register<AnsiTextBlock, IReadOnlyList<AnsiRun>?>(nameof(Runs));

    private static readonly IBrush?[] Palette =
    [
        null, // 0 black: keep the theme foreground (black on a dark theme is unreadable)
        new SolidColorBrush(Color.Parse("#D0453B")), new SolidColorBrush(Color.Parse("#2E9E4F")), new SolidColorBrush(Color.Parse("#B8860B")),
        new SolidColorBrush(Color.Parse("#3F7BD8")), new SolidColorBrush(Color.Parse("#B455B6")), new SolidColorBrush(Color.Parse("#1E9DAA")),
        null, // 7 white: theme foreground
        new SolidColorBrush(Color.Parse("#8A8F98")), new SolidColorBrush(Color.Parse("#EF6A5F")), new SolidColorBrush(Color.Parse("#46C06E")),
        new SolidColorBrush(Color.Parse("#D9A520")), new SolidColorBrush(Color.Parse("#6A9CF0")), new SolidColorBrush(Color.Parse("#D07AD2")),
        new SolidColorBrush(Color.Parse("#3FC0CC")), null,
    ];

    static AnsiTextBlock() => RunsProperty.Changed.AddClassHandler<AnsiTextBlock>((b, _) => b.Rebuild());

    public IReadOnlyList<AnsiRun>? Runs { get => GetValue(RunsProperty); set => SetValue(RunsProperty, value); }

    protected override Type StyleKeyOverride => typeof(SelectableTextBlock);

    private void Rebuild()
    {
        Inlines?.Clear();
        if (Runs is not { Count: > 0 } runs) return;
        Inlines ??= new InlineCollection();
        foreach (var r in runs)
        {
            var run = new Run(r.Text);
            if (r.Color is { } c && c is >= 0 and < 16 && Palette[c] is { } brush) run.Foreground = brush;
            if (r.Bold) run.FontWeight = FontWeight.Bold;
            Inlines.Add(run);
        }
    }
}
