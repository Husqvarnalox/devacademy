using System.Globalization;

namespace DevAcademy.Application.Visualization;

/// <summary>2-D point sets for the k-means and PCA visualisers: parsing of the Markdown body and the built-in deterministic datasets.</summary>
public static class PointData
{
    public const int MaxPoints = 500;
    public const double MaxCoordinate = 1e6;

    /// <summary>
    /// Parses "x y" lines (blank lines are skipped). An empty body is valid and yields no points, so the caller falls back to a built-in dataset.
    /// </summary>
    public static bool TryParse(string? body, out IReadOnlyList<(double X, double Y)> points, out string? error)
    {
        var list = new List<(double, double)>();
        points = list;
        error = null;
        int lineNo = 0;
        foreach (string raw in (body ?? "").Split('\n'))
        {
            lineNo++;
            string line = raw.Trim();
            if (line.Length == 0) continue;
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2
                || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double x)
                || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double y)
                || !double.IsFinite(x) || !double.IsFinite(y) || Math.Abs(x) > MaxCoordinate || Math.Abs(y) > MaxCoordinate)
            {
                error = $"line {lineNo}: expected two numbers 'x y', got '{line}'";
                return false;
            }
            list.Add((x, y));
            if (list.Count > MaxPoints)
            {
                error = $"too many points (at most {MaxPoints})";
                return false;
            }
        }
        return true;
    }

    /// <summary>Three well-separated Gaussian blobs (20 points each); the same points on every run.</summary>
    public static IReadOnlyList<(double X, double Y)> DefaultBlobs()
    {
        var rng = new Random(7);
        (double X, double Y)[] centers = [(-4, -2.5), (0.5, 4), (5, -1.5)];
        var points = new List<(double, double)>();
        foreach (var (cx, cy) in centers)
            for (int i = 0; i < 20; i++)
            {
                (double gx, double gy) = Gaussian(rng);
                points.Add((Math.Round(cx + 0.9 * gx, 2), Math.Round(cy + 0.9 * gy, 2)));
            }
        return points;
    }

    /// <summary>An elongated, tilted cloud (strong correlation, a small spread across the main direction); 40 points.</summary>
    public static IReadOnlyList<(double X, double Y)> DefaultCorrelated()
    {
        var rng = new Random(3);
        var points = new List<(double, double)>();
        double cos = Math.Cos(0.5), sin = Math.Sin(0.5);
        for (int i = 0; i < 40; i++)
        {
            (double gx, double gy) = Gaussian(rng);
            double u = 3.0 * gx, v = 0.8 * gy;
            points.Add((Math.Round(2 + u * cos - v * sin, 2), Math.Round(1 + u * sin + v * cos, 2)));
        }
        return points;
    }

    private static (double, double) Gaussian(Random rng)
    {
        // Box-Muller; 1 - NextDouble() is in (0, 1] so the logarithm is finite.
        double r = Math.Sqrt(-2 * Math.Log(1 - rng.NextDouble()));
        double a = 2 * Math.PI * rng.NextDouble();
        return (r * Math.Cos(a), r * Math.Sin(a));
    }
}
