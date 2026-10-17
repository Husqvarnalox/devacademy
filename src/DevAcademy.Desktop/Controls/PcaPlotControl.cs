using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using DevAcademy.Application.Visualization;
using DevAcademy.Desktop.ViewModels;

namespace DevAcademy.Desktop.Controls;

/// <summary>
/// The PCA stages: points, the centre cross, the principal axes (half-length 2·sqrt(eigenvalue)), dashed projection lines to the feet on PC1 and the
/// 1-D reconstruction. The scale is fixed by the whole trace, so the picture does not rescale between stages.
/// </summary>
public sealed class PcaPlotControl : Control
{
    public static readonly StyledProperty<PcaFrame?> FrameProperty = AvaloniaProperty.Register<PcaPlotControl, PcaFrame?>(nameof(Frame));

    private const double Pad = 18;

    public PcaFrame? Frame { get => GetValue(FrameProperty); set => SetValue(FrameProperty, value); }

    static PcaPlotControl()
    {
        AffectsRender<PcaPlotControl>(FrameProperty);
        FrameProperty.Changed.AddClassHandler<PcaPlotControl>((c, _) => c.UpdateAutomation());
    }

    public PcaPlotControl()
    {
        MinHeight = 240;
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    private void UpdateAutomation()
    {
        if (Frame is { } f) AutomationProperties.SetHelpText(this, f.Current.Stage.ToString());
    }

    private IBrush Brush(string key, string fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var v) && v is IBrush b ? b : new SolidColorBrush(Color.Parse(fallback));

    private static (double X, double Y)[] AxisEnds((double X, double Y) origin, (double X, double Y) dir, double eigenvalue)
    {
        double h = 2 * Math.Sqrt(eigenvalue);
        return [(origin.X - h * dir.X, origin.Y - h * dir.Y), (origin.X + h * dir.X, origin.Y + h * dir.Y)];
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Frame is not { } frame) return;
        PcaResult r = frame.Trace.Result;
        PcaStep step = frame.Current;
        var rect = new Rect(Pad, Pad, Math.Max(10, Bounds.Width - 2 * Pad), Math.Max(10, Bounds.Height - 2 * Pad));
        IBrush line = Brush("LineBrush", "#DCDCD7");
        IBrush muted = Brush("MutedText", "#6A6A64");
        IBrush accent = Brush("AccentText", "#1F5C9E");
        IBrush pc1 = Brush("VizSeries2", "#C24E00");
        IBrush pc2 = Brush("VizSeries3", "#00806A");
        IBrush panel = Brush("PanelBrush", "#FFFFFF");
        var typeface = new Typeface(FontFamily.Default);

        // One scale for every stage: raw and centred data, projections, the reconstruction and the longest axes (taken at both origins).
        var extent = new List<(double X, double Y)>();
        foreach (var s in frame.Trace.Steps)
        {
            extent.AddRange(s.Points);
            if (s.Ghost is { } g) extent.AddRange(g);
            if (s.Feet is { } f) extent.AddRange(f);
        }
        foreach (var o in new[] { (0.0, 0.0), r.Mean })
        {
            extent.AddRange(AxisEnds(o, r.Pc1, r.Eigenvalue1));
            extent.AddRange(AxisEnds(o, r.Pc2, r.Eigenvalue2));
        }
        var map = PlotMap.Fit(extent, rect);

        context.DrawRectangle(panel, new Pen(line, 1), rect);

        if (step.AxesOrigin is { } o2)
        {
            Point c = map.To(o2);
            context.DrawLine(new Pen(muted, 1), new Point(c.X - 6, c.Y), new Point(c.X + 6, c.Y));
            context.DrawLine(new Pen(muted, 1), new Point(c.X, c.Y - 6), new Point(c.X, c.Y + 6));
        }

        if (step.Ghost is { } ghost)
            foreach (var p in ghost) context.DrawEllipse(muted, null, map.To(p), 3, 3);

        if (step.Feet is { } feet)
        {
            var dash = new Pen(muted, 1.2, new DashStyle([3, 3], 0));
            for (int i = 0; i < feet.Length; i++) context.DrawLine(dash, map.To(step.Points[i]), map.To(feet[i]));
        }

        if (step.Stage >= PcaStage.Axes && step.AxesOrigin is { } origin)
        {
            DrawAxis(context, map, AxisEnds(origin, r.Pc1, r.Eigenvalue1), pc1, "PC1", typeface);
            DrawAxis(context, map, AxisEnds(origin, r.Pc2, r.Eigenvalue2), pc2, "PC2", typeface);
        }

        if (step.Feet is { } f2)
            foreach (var p in f2) context.DrawEllipse(pc1, null, map.To(p), 3.5, 3.5);

        foreach (var p in step.Points)
            context.DrawEllipse(accent, new Pen(panel, 0.8), map.To(p), 4, 4);
    }

    private static void DrawAxis(DrawingContext context, PlotMap map, (double X, double Y)[] ends, IBrush brush, string label, Typeface typeface)
    {
        Point a = map.To(ends[0]), b = map.To(ends[1]);
        context.DrawLine(new Pen(brush, 2.5, lineCap: PenLineCap.Round), a, b);
        var text = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 12, brush);
        context.DrawText(text, new Point(b.X + 4, b.Y - 7));
    }
}
