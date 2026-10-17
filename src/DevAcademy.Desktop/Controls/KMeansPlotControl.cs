using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using DevAcademy.Application.Visualization;
using DevAcademy.Desktop.ViewModels;

namespace DevAcademy.Desktop.Controls;

/// <summary>
/// Scatter plot coloured by cluster, the centroid trail and the current centroids (diamonds). Everything drawn is a recorded frame of the trace.
/// Cluster colours come from the theme's VizSeries brushes, so they stay readable in light and dark.
/// </summary>
public sealed class KMeansPlotControl : Control
{
    public static readonly StyledProperty<KMeansFrame?> FrameProperty = AvaloniaProperty.Register<KMeansPlotControl, KMeansFrame?>(nameof(Frame));

    private const double Pad = 14;

    public KMeansFrame? Frame { get => GetValue(FrameProperty); set => SetValue(FrameProperty, value); }

    static KMeansPlotControl()
    {
        AffectsRender<KMeansPlotControl>(FrameProperty);
        FrameProperty.Changed.AddClassHandler<KMeansPlotControl>((c, _) => c.UpdateAutomation());
    }

    public KMeansPlotControl()
    {
        MinHeight = 240;
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    private void UpdateAutomation()
    {
        if (Frame is not { } f) return;
        KMeansStep s = f.Current;
        AutomationProperties.SetHelpText(this, string.Create(CultureInfo.InvariantCulture,
            $"iteration {s.Iteration}, {s.Phase}: centroids {string.Join(" ", s.Centroids.Select(c => $"({c.X:0.##}, {c.Y:0.##})"))}, inertia {s.Inertia:0.###}"));
    }

    private IBrush Brush(string key, string fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var v) && v is IBrush b ? b : new SolidColorBrush(Color.Parse(fallback));

    private static readonly string[] SeriesFallback = ["#0072B2", "#C24E00", "#00806A", "#B0457E", "#8A6F00", "#6B4FBB"];

    private IBrush Series(int cluster) => Brush("VizSeries" + (cluster % 6 + 1), SeriesFallback[cluster % 6]);

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Frame is not { } frame) return;
        KMeansTrace trace = frame.Trace;
        KMeansStep cur = frame.Current;
        var rect = new Rect(Pad, Pad, Math.Max(10, Bounds.Width - 2 * Pad), Math.Max(10, Bounds.Height - 2 * Pad));
        IBrush line = Brush("LineBrush", "#DCDCD7");
        IBrush muted = Brush("MutedText", "#6A6A64");
        IBrush ink = Brush("TextBrush", "#1D1C1A");
        var map = PlotMap.Fit(trace.Points, rect);

        context.DrawRectangle(Brush("SurfaceBrush", "#FFFFFF"), new Pen(line, 1), rect);

        // Trail: positions the centroids had in the initial frame and after each update step up to this frame.
        var path = trace.Steps.Take(frame.Position + 1).Where(s => s.Phase != KMeansPhase.Assign).ToList();
        for (int j = 0; j < trace.K; j++)
        {
            var pen = new Pen(Series(j), 1.6, new DashStyle([3, 3], 0));
            for (int i = 1; i < path.Count; i++)
                context.DrawLine(pen, map.To(path[i - 1].Centroids[j]), map.To(path[i].Centroids[j]));
        }

        for (int i = 0; i < trace.Points.Count; i++)
        {
            int c = cur.Assignments[i];
            IBrush fill = c < 0 ? muted : Series(c);
            context.DrawEllipse(fill, new Pen(Brush("PanelBrush", "#FFFFFF"), 0.8), map.To(trace.Points[i]), 4, 4);
        }

        for (int j = 0; j < trace.K; j++)
        {
            Point p = map.To(cur.Centroids[j]);
            const double r = 9;
            var diamond = new StreamGeometry();
            using (var g = diamond.Open())
            {
                g.BeginFigure(new Point(p.X, p.Y - r), true);
                g.LineTo(new Point(p.X + r, p.Y));
                g.LineTo(new Point(p.X, p.Y + r));
                g.LineTo(new Point(p.X - r, p.Y));
                g.EndFigure(true);
            }
            context.DrawGeometry(Series(j), new Pen(ink, 2), diamond);
        }
    }
}
