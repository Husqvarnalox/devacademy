namespace DevAcademy.Application.Visualization;

public enum PcaStage { Raw, Centered, Axes, Projection, Reconstruction }

/// <summary>
/// PCA of 2-D data in closed form. Variances use ddof = 1 (as the lecture's <c>np.cov</c>). <see cref="Pc1"/> and <see cref="Pc2"/> are unit vectors with a
/// deterministic sign: the component of larger magnitude is positive (the first one on a tie). When all points coincide
/// (<see cref="IsDegenerate"/>) the axes default to the coordinate axes and both ratios are 0.
/// </summary>
public sealed record PcaResult(
    (double X, double Y) Mean, (double X, double Y)[] Centered,
    double CovXx, double CovXy, double CovYy,
    double Eigenvalue1, double Eigenvalue2, (double X, double Y) Pc1, (double X, double Y) Pc2,
    double Ratio1, double Ratio2, double[] Scores, bool IsDegenerate)
{
    /// <summary>Feet of the perpendiculars on the PC1 line, in centred coordinates.</summary>
    public (double X, double Y)[] Feet => Scores.Select(t => (t * Pc1.X, t * Pc1.Y)).ToArray();

    /// <summary>The rank-1 reconstruction μ + t·v1 in the original coordinates.</summary>
    public (double X, double Y)[] Reconstruction => Scores.Select(t => (Mean.X + t * Pc1.X, Mean.Y + t * Pc1.Y)).ToArray();

    /// <summary>Mean squared distance between the points and their reconstructions.</summary>
    public double ReconstructionError => Centered.Zip(Scores, (c, t) => (c.X - t * Pc1.X) * (c.X - t * Pc1.X) + (c.Y - t * Pc1.Y) * (c.Y - t * Pc1.Y)).Average();
}

/// <summary>What one stage shows. Coordinates are data coordinates; the optional layers are drawn only when not null.</summary>
public sealed record PcaStep(
    PcaStage Stage, (double X, double Y)[] Points, (double X, double Y)[]? Ghost, (double X, double Y)[]? Feet, (double X, double Y)? AxesOrigin);

public sealed record PcaTrace(PcaResult Result, IReadOnlyList<PcaStep> Steps);

public static class PcaLab
{
    private const double DegenerateVariance = 1e-12;

    public static PcaResult Analyse(IReadOnlyList<(double X, double Y)> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count < 2) throw new ArgumentException("PCA needs at least 2 points.", nameof(points));
        int n = points.Count;
        double mx = points.Average(p => p.X), my = points.Average(p => p.Y);
        var centered = points.Select(p => (p.X - mx, p.Y - my)).ToArray();
        double a = centered.Sum(p => p.Item1 * p.Item1) / (n - 1);
        double b = centered.Sum(p => p.Item1 * p.Item2) / (n - 1);
        double c = centered.Sum(p => p.Item2 * p.Item2) / (n - 1);

        // Symmetric 2x2 [[a, b], [b, c]]: λ = (a+c)/2 ± sqrt(((a-c)/2)² + b²).
        double mid = (a + c) / 2, rad = Math.Sqrt(((a - c) / 2) * ((a - c) / 2) + b * b);
        double l1 = mid + rad, l2 = Math.Max(0, mid - rad);
        bool degenerate = l1 <= DegenerateVariance;

        (double X, double Y) v1 = (1, 0);
        if (!degenerate)
        {
            if (Math.Abs(b) <= 1e-15 * Math.Max(1, Math.Abs(l1))) v1 = a >= c ? (1, 0) : (0, 1);
            else
            {
                // (A - λI)v = 0 gives v ∝ (b, λ - a) or (λ - c, b); take the better conditioned one.
                (double X, double Y) u = (b, l1 - a), w = (l1 - c, b);
                v1 = u.X * u.X + u.Y * u.Y >= w.X * w.X + w.Y * w.Y ? u : w;
            }
            v1 = Normalise(v1);
        }
        (double X, double Y) v2 = Canonical((-v1.Y, v1.X));
        v1 = Canonical(v1);

        var scores = centered.Select(p => p.Item1 * v1.X + p.Item2 * v1.Y).ToArray();
        double total = l1 + l2;
        return new PcaResult((mx, my), centered, a, b, c, degenerate ? 0 : l1, degenerate ? 0 : l2, v1, v2,
            degenerate ? 0 : l1 / total, degenerate ? 0 : l2 / total, scores, degenerate);
    }

    /// <summary>The five stages: raw data, centred data, principal axes, projection on PC1, 1-D reconstruction.</summary>
    public static PcaTrace Run(IReadOnlyList<(double X, double Y)> points)
    {
        PcaResult r = Analyse(points);
        var raw = points.ToArray();
        var feet = r.Feet;
        var steps = new List<PcaStep>
        {
            new(PcaStage.Raw, raw, null, null, null),
            new(PcaStage.Centered, r.Centered, null, null, (0, 0)),
            new(PcaStage.Axes, r.Centered, null, null, (0, 0)),
            new(PcaStage.Projection, r.Centered, null, feet, (0, 0)),
            new(PcaStage.Reconstruction, r.Reconstruction, raw, null, r.Mean),
        };
        return new PcaTrace(r, steps);
    }

    private static (double X, double Y) Normalise((double X, double Y) v)
    {
        double len = Math.Sqrt(v.X * v.X + v.Y * v.Y);
        return (v.X / len, v.Y / len);
    }

    /// <summary>Sign convention: the component of larger magnitude is positive; on a tie the x component is.</summary>
    private static (double X, double Y) Canonical((double X, double Y) v)
    {
        bool flip = Math.Abs(Math.Abs(v.X) - Math.Abs(v.Y)) <= 1e-12 ? v.X < 0 : (Math.Abs(v.X) > Math.Abs(v.Y) ? v.X < 0 : v.Y < 0);
        return flip ? (-v.X, -v.Y) : v;
    }
}
