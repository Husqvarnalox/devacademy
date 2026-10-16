using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DevAcademy.Desktop.ViewModels;

namespace DevAcademy.Desktop.Controls;

/// <summary>Draws one <see cref="SortFrame"/> as bars. Colours come from theme resources; no animation of its own.</summary>
public sealed class SortBarsControl : Control
{
    public static readonly StyledProperty<SortFrame?> FrameProperty = AvaloniaProperty.Register<SortBarsControl, SortFrame?>(nameof(Frame));
    public static readonly StyledProperty<double> MaxValueProperty = AvaloniaProperty.Register<SortBarsControl, double>(nameof(MaxValue), 99);

    public SortFrame? Frame { get => GetValue(FrameProperty); set => SetValue(FrameProperty, value); }
    public double MaxValue { get => GetValue(MaxValueProperty); set => SetValue(MaxValueProperty, value); }

    static SortBarsControl()
    {
        AffectsRender<SortBarsControl>(FrameProperty, MaxValueProperty);
        FrameProperty.Changed.AddClassHandler<SortBarsControl>((c, _) => c.UpdateAutomation());
    }

    public SortBarsControl()
    {
        MinHeight = 160;
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    private void UpdateAutomation() =>
        Avalonia.Automation.AutomationProperties.SetHelpText(this, Frame is null ? "" : string.Join(" ", Frame.Values));

    private IBrush Brush(string key, string fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var v) && v is IBrush b ? b : new SolidColorBrush(Color.Parse(fallback));

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Frame is not { Values.Count: > 0 } f) return;
        var n = f.Values.Count;
        var w = Bounds.Width;
        var h = Bounds.Height;
        var gap = n > 24 ? 2 : 4;
        var barW = Math.Max(1, (w - gap * (n - 1)) / n);
        var normal = Brush("AccentText", "#2563A8");
        var compared = Brush("WarnBrush", "#8A5A00");
        var swapped = Brush("DangerBrush", "#B3261E");
        var sorted = Brush("SuccessBrush", "#2E7D32");
        var muted = Brush("MutedText", "#6A6A64");
        var text = Brush("MutedText", "#6A6A64");
        var typeface = new Typeface(FontFamily.Default);
        var labelRoom = barW >= 18 ? 16.0 : 0.0;
        var plotH = Math.Max(1, h - labelRoom);

        for (var i = 0; i < n; i++)
        {
            var v = f.Values[i];
            var bh = Math.Max(2, plotH * v / Math.Max(1, MaxValue));
            var x = i * (barW + gap);
            IBrush fill = normal;
            if (f.Sorted.Contains(i)) fill = sorted;
            if (f.Pivot == i) fill = muted;
            if (f.Compared.Contains(i)) fill = compared;
            if (f.Swapped.Contains(i) || f.Written == i) fill = swapped;
            var inRange = f.Range is not { } r || (i >= r.Lo && i <= r.Hi);
            using (context.PushOpacity(inRange ? 1.0 : 0.35))
                context.DrawRectangle(fill, null, new Rect(x, plotH - bh, barW, bh));
            if (labelRoom > 0)
            {
                var ft = new FormattedText(v.ToString(), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 11, text);
                context.DrawText(ft, new Point(x + (barW - ft.Width) / 2, plotH + 1));
            }
        }
    }
}
