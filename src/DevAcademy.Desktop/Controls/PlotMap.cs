using Avalonia;

namespace DevAcademy.Desktop.Controls;

/// <summary>World-to-screen mapping for the 2-D scatter plots: fits the data into a rectangle with equal scale on both axes (orthogonal stays orthogonal).</summary>
internal readonly struct PlotMap
{
    private readonly double _cx, _cy, _scale;
    private readonly Rect _rect;

    private PlotMap(Rect rect, double cx, double cy, double scale)
    {
        _rect = rect;
        _cx = cx;
        _cy = cy;
        _scale = scale;
    }

    public static PlotMap Fit(IEnumerable<(double X, double Y)> points, Rect rect)
    {
        double x0 = double.MaxValue, x1 = double.MinValue, y0 = double.MaxValue, y1 = double.MinValue;
        foreach (var (x, y) in points)
        {
            x0 = Math.Min(x0, x); x1 = Math.Max(x1, x);
            y0 = Math.Min(y0, y); y1 = Math.Max(y1, y);
        }
        if (x0 > x1) { x0 = y0 = -1; x1 = y1 = 1; }
        double w = Math.Max(x1 - x0, 1e-9), h = Math.Max(y1 - y0, 1e-9);
        double scale = Math.Min(rect.Width / (w * 1.12), rect.Height / (h * 1.12));
        return new PlotMap(rect, (x0 + x1) / 2, (y0 + y1) / 2, scale);
    }

    public Point To(double x, double y) => new(_rect.Center.X + (x - _cx) * _scale, _rect.Center.Y - (y - _cy) * _scale);

    public Point To((double X, double Y) p) => To(p.X, p.Y);
}
