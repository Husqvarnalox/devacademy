using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DevAcademy.Desktop.ViewModels;

namespace DevAcademy.Desktop.Controls;

/// <summary>Loss history of the trace up to the current position (log scale when every loss is positive).</summary>
public sealed class LossChartControl : Control
{
    public static readonly StyledProperty<GdFrame?> FrameProperty = AvaloniaProperty.Register<LossChartControl, GdFrame?>(nameof(Frame));

    public GdFrame? Frame { get => GetValue(FrameProperty); set => SetValue(FrameProperty, value); }

    static LossChartControl() => AffectsRender<LossChartControl>(FrameProperty);

    public LossChartControl()
    {
        MinHeight = 110;
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    private IBrush Brush(string key, string fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var v) && v is IBrush b ? b : new SolidColorBrush(Color.Parse(fallback));

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Frame is not { } frame) return;
        var steps = frame.Trace.Steps;
        var r = new Rect(64, 6, Math.Max(10, Bounds.Width - 72), Math.Max(10, Bounds.Height - 26));
        IBrush axis = Brush("MutedText", "#6A6A64");
        IBrush line = Brush("AccentText", "#2563A8");
        IBrush current = Brush("DangerBrush", "#B3261E");
        var typeface = new Typeface(FontFamily.Default);

        var finite = steps.Select(s => s.Loss).Where(double.IsFinite).ToList();
        bool log = finite.Count > 0 && finite.All(v => v > 0);
        double T(double v) => log ? Math.Log10(v) : v;
        if (finite.Count == 0) return;
        double lo = finite.Min(T), hi = finite.Max(T);
        if (hi - lo < 1e-12) { hi += 1; lo -= 1; }

        int total = Math.Max(1, steps.Count - 1);
        Point Map(int k, double loss) => new(r.X + (double)k / total * r.Width, r.Bottom - (Math.Clamp(T(loss), lo, hi) - lo) / (hi - lo) * r.Height);

        context.DrawRectangle(Brush("SurfaceBrush", "#FFFFFF"), new Pen(Brush("LineBrush", "#E2DED5"), 1), r);
        var pen = new Pen(line, 1.8);
        Point? prev = null;
        for (int k = 0; k <= frame.Position; k++)
        {
            if (!double.IsFinite(steps[k].Loss)) break;
            Point p = Map(k, steps[k].Loss);
            if (prev is { } q) context.DrawLine(pen, q, p);
            prev = p;
        }
        if (prev is { } cur) context.DrawEllipse(current, null, cur, 4, 4);

        string top = log ? $"10^{hi:0.#}" : hi.ToString("0.##", CultureInfo.InvariantCulture);
        string bottom = log ? $"10^{lo:0.#}" : lo.ToString("0.##", CultureInfo.InvariantCulture);
        context.DrawText(new FormattedText(top, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 12, axis), new Point(2, r.Y));
        context.DrawText(new FormattedText(bottom, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 12, axis), new Point(2, r.Bottom - 14));
        context.DrawText(new FormattedText("0", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 12, axis), new Point(r.X, r.Bottom + 3));
        context.DrawText(new FormattedText(total.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 12, axis), new Point(r.Right - 28, r.Bottom + 3));
    }
}
