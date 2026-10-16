using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using DevAcademy.Application.Visualization;
using DevAcademy.Desktop.ViewModels;

namespace DevAcademy.Desktop.Controls;

/// <summary>
/// Draws the function (banded heatmap for 2-D, curve for 1-D), the minima, and the path of iterates up to the frame's position.
/// Pressing the plot reports the world coordinates as the new start point. Everything drawn comes from the trace, nothing is interpolated.
/// </summary>
public sealed class GradientPlotControl : Control
{
    public static readonly StyledProperty<GdFrame?> FrameProperty = AvaloniaProperty.Register<GradientPlotControl, GdFrame?>(nameof(Frame));

    private const int Grid = 56;
    private const int Bands = 16;
    private const double Pad = 26;

    private GdFunction? _cachedFunction;
    private double[,]? _bands; // normalised 0..1 band index per cell (2-D)
    private (double Min, double Max)[]? _curveRange;

    public GdFrame? Frame { get => GetValue(FrameProperty); set => SetValue(FrameProperty, value); }

    /// <summary>Raised with world coordinates (x, y) when the user presses inside the plot.</summary>
    public event Action<double, double>? StartPicked;

    static GradientPlotControl()
    {
        AffectsRender<GradientPlotControl>(FrameProperty);
        FrameProperty.Changed.AddClassHandler<GradientPlotControl>((c, _) => c.UpdateAutomation());
    }

    public GradientPlotControl()
    {
        MinHeight = 220;
        Cursor = new Cursor(StandardCursorType.Cross);
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    private void UpdateAutomation()
    {
        if (Frame is not { } f) return;
        GdStep s = f.Current;
        Avalonia.Automation.AutomationProperties.SetHelpText(this,
            string.Create(CultureInfo.InvariantCulture, $"step {s.Step}: ({string.Join(", ", s.Parameters.Select(p => p.ToString("0.###", CultureInfo.InvariantCulture)))}) loss {s.Loss:0.####}"));
    }

    private IBrush Brush(string key, string fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var v) && v is IBrush b ? b : new SolidColorBrush(Color.Parse(fallback));

    private Rect PlotRect => new(Pad, 6, Math.Max(10, Bounds.Width - Pad - 8), Math.Max(10, Bounds.Height - Pad - 6));

    private Point ToScreen(GdFunction f, double x, double y, Rect r)
    {
        var b = f.Bounds;
        double px = r.X + (x - b.XMin) / (b.XMax - b.XMin) * r.Width;
        double py;
        if (f.Dimension > 1) py = r.Bottom - (y - b.YMin) / (b.YMax - b.YMin) * r.Height;
        else
        {
            (double min, double max) = CurveRange(f);
            py = r.Bottom - (y - min) / (max - min) * r.Height;
        }
        return new Point(px, py);
    }

    private (double Min, double Max) CurveRange(GdFunction f)
    {
        _curveRange ??= [CurveExtent(f)];
        return _curveRange[0];
    }

    private static (double, double) CurveExtent(GdFunction f)
    {
        double min = double.MaxValue, max = double.MinValue;
        for (int i = 0; i <= 200; i++)
        {
            double v = f.Value([f.Bounds.XMin + (f.Bounds.XMax - f.Bounds.XMin) * i / 200]);
            min = Math.Min(min, v);
            max = Math.Max(max, v);
        }
        double pad = (max - min) * 0.08;
        return (min - pad, max + pad);
    }

    private void EnsureCache(GdFunction f)
    {
        if (ReferenceEquals(_cachedFunction, f)) return;
        _cachedFunction = f;
        _curveRange = null;
        _bands = null;
        if (f.Dimension < 2) return;
        var b = f.Bounds;
        var values = new double[Grid, Grid];
        double lo = double.MaxValue, hi = double.MinValue;
        for (int i = 0; i < Grid; i++)
            for (int j = 0; j < Grid; j++)
            {
                double x = b.XMin + (i + 0.5) / Grid * (b.XMax - b.XMin);
                double y = b.YMin + (j + 0.5) / Grid * (b.YMax - b.YMin);
                double v = f.Value([x, y]);
                values[i, j] = v;
                lo = Math.Min(lo, v);
                hi = Math.Max(hi, v);
            }
        // log scale: the interesting structure sits near the minimum, where the values are tiny against the corners
        double shift = 1 - lo;
        double range = Math.Log(hi + shift);
        _bands = new double[Grid, Grid];
        for (int i = 0; i < Grid; i++)
            for (int j = 0; j < Grid; j++)
                _bands[i, j] = Math.Floor(Math.Log(values[i, j] + shift) / range * Bands) / Bands;
    }

    private static Color Palette(double t)
    {
        // dark blue -> teal -> yellow; readable on light and dark themes
        t = Math.Clamp(t, 0, 1);
        byte R(double a, double b) => (byte)(a + (b - a) * t);
        return t < 0.5
            ? Color.FromRgb((byte)(40 + 20 * t * 2), (byte)(60 + 110 * t * 2), (byte)(140 + 20 * t * 2))
            : Color.FromRgb(R(60, 245), R(170, 220), (byte)(160 - 110 * (t - 0.5) * 2));
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Frame is not { } frame) return;
        GdFunction f = frame.Trace.Function;
        EnsureCache(f);
        Rect r = PlotRect;
        IBrush axis = Brush("MutedText", "#6A6A64");
        IBrush pathBrush = Brush("AccentText", "#2563A8");
        IBrush current = Brush("DangerBrush", "#B3261E");
        IBrush minimum = Brush("SuccessBrush", "#2E7D32");
        var typeface = new Typeface(FontFamily.Default);

        using (context.PushClip(r))
        {
            if (f.Dimension > 1 && _bands is not null)
            {
                double cw = r.Width / Grid, ch = r.Height / Grid;
                for (int i = 0; i < Grid; i++)
                    for (int j = 0; j < Grid; j++)
                        context.DrawRectangle(new SolidColorBrush(Palette(_bands[i, j])), null,
                            new Rect(r.X + i * cw, r.Bottom - (j + 1) * ch, cw + 0.6, ch + 0.6));
            }
            else
            {
                var pen = new Pen(axis, 2);
                Point? prev = null;
                for (int i = 0; i <= 200; i++)
                {
                    double x = f.Bounds.XMin + (f.Bounds.XMax - f.Bounds.XMin) * i / 200;
                    Point p = ToScreen(f, x, f.Value([x]), r);
                    if (prev is { } q) context.DrawLine(pen, q, p);
                    prev = p;
                }
            }

            foreach (double[] m in f.Minima)
            {
                Point p = ToScreen(f, m[0], f.Dimension > 1 ? m[1] : f.Value(m), r);
                context.DrawEllipse(null, new Pen(minimum, 2), p, 6, 6);
            }

            // Path: segments between consecutive trace steps, drawn only up to the current position.
            var line = new Pen(pathBrush, 1.8);
            var white = new Pen(Brushes.White, 3.4);
            Point? last = null;
            for (int k = 0; k <= frame.Position; k++)
            {
                GdStep s = frame.Trace.Steps[k];
                if (!double.IsFinite(s.Loss) || s.Parameters.Any(v => !double.IsFinite(v))) break;
                Point p = ToScreen(f, s.Parameters[0], f.Dimension > 1 ? s.Parameters[1] : s.Loss, r);
                if (last is { } q)
                {
                    context.DrawLine(white, q, p);
                    context.DrawLine(line, q, p);
                }
                context.DrawEllipse(pathBrush, new Pen(Brushes.White, 1), p, 3, 3);
                last = p;
            }
            if (last is { } cur) context.DrawEllipse(current, new Pen(Brushes.White, 1.5), cur, 6, 6);
        }

        context.DrawRectangle(null, new Pen(axis, 1), r);
        DrawLabel(context, typeface, axis, f.Bounds.XMin.ToString("0.#", CultureInfo.InvariantCulture), new Point(r.X, r.Bottom + 4));
        DrawLabel(context, typeface, axis, f.Bounds.XMax.ToString("0.#", CultureInfo.InvariantCulture), new Point(r.Right - 18, r.Bottom + 4));
        if (f.Dimension > 1)
        {
            DrawLabel(context, typeface, axis, f.Bounds.YMax.ToString("0.#", CultureInfo.InvariantCulture), new Point(2, r.Y));
            DrawLabel(context, typeface, axis, f.Bounds.YMin.ToString("0.#", CultureInfo.InvariantCulture), new Point(2, r.Bottom - 14));
        }
    }

    private static void DrawLabel(DrawingContext c, Typeface tf, IBrush brush, string text, Point at) =>
        c.DrawText(new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, tf, 11, brush), at);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Frame is not { } frame || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        GdFunction f = frame.Trace.Function;
        Rect r = PlotRect;
        Point p = e.GetPosition(this);
        if (!r.Contains(p)) return;
        var b = f.Bounds;
        double x = b.XMin + (p.X - r.X) / r.Width * (b.XMax - b.XMin);
        double y = f.Dimension > 1 ? b.YMin + (r.Bottom - p.Y) / r.Height * (b.YMax - b.YMin) : 0;
        StartPicked?.Invoke(x, y);
        e.Handled = true;
    }
}
