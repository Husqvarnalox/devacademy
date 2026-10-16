using System.Globalization;

namespace DevAcademy.Application.Visualization;

public enum GdOptimizerKind { Gd, Momentum, Adam }

public enum GdStopReason
{
    /// <summary>Ran all requested steps without meeting another stop rule.</summary>
    MaxSteps,
    /// <summary>Gradient norm fell to the tolerance.</summary>
    Converged,
    /// <summary>Loss or parameters became NaN/Infinity, or the loss exploded.</summary>
    Diverged,
}

/// <summary>
/// A test function with an analytic gradient. 1-D functions use a single coordinate; 2-D ones use (x, y).
/// <see cref="Lipschitz"/> is the gradient's Lipschitz constant when it is global (quadratic bowl), else null.
/// </summary>
public sealed class GdFunction
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Formula { get; init; }
    public required int Dimension { get; init; }
    public required Func<double[], double> Value { get; init; }
    public required Func<double[], double[]> Gradient { get; init; }
    /// <summary>Plot window: x0, x1, y0, y1 (y is unused for 1-D).</summary>
    public required (double XMin, double XMax, double YMin, double YMax) Bounds { get; init; }
    public required double[] DefaultStart { get; init; }
    /// <summary>Known minimisers (for display and tests); the double-well has two, the first is global.</summary>
    public required IReadOnlyList<double[]> Minima { get; init; }
    public double? Lipschitz { get; init; }
}

/// <summary>The built-in functions. Each gradient is analytic; the tests compare them with central finite differences.</summary>
public static class GdFunctions
{
    public const string QuadraticId = "quadratic";
    public const string RosenbrockId = "rosenbrock";
    public const string DoubleWellId = "double-well";

    public static IReadOnlyList<string> Ids { get; } = [QuadraticId, RosenbrockId, DoubleWellId];

    /// <summary>f(x,y) = ½(x² + κ·y²). Condition number κ ≥ 1 sets how elongated the bowl is; L = κ; the minimum is (0,0).</summary>
    public static GdFunction Quadratic(double conditioning = 10)
    {
        if (!double.IsFinite(conditioning) || conditioning < 1)
            throw new ArgumentOutOfRangeException(nameof(conditioning), "Conditioning must be a finite number >= 1.");
        double k = conditioning;
        return new GdFunction
        {
            Id = QuadraticId,
            Title = "Квадратичная чаша",
            Formula = $"f(x,y) = ½(x² + {k.ToString("0.##", CultureInfo.InvariantCulture)}·y²)",
            Dimension = 2,
            Value = p => 0.5 * (p[0] * p[0] + k * p[1] * p[1]),
            Gradient = p => [p[0], k * p[1]],
            Bounds = (-3, 3, -3, 3),
            DefaultStart = [2.5, 1.5],
            Minima = [[0, 0]],
            Lipschitz = k,
        };
    }

    /// <summary>f(x,y) = (1−x)² + 100(y−x²)². Narrow curved valley; the minimum is (1,1).</summary>
    public static GdFunction Rosenbrock() => new()
    {
        Id = RosenbrockId,
        Title = "Функция Розенброка",
        Formula = "f(x,y) = (1−x)² + 100(y−x²)²",
        Dimension = 2,
        Value = p => Math.Pow(1 - p[0], 2) + 100 * Math.Pow(p[1] - p[0] * p[0], 2),
        Gradient = p => [-2 * (1 - p[0]) - 400 * p[0] * (p[1] - p[0] * p[0]), 200 * (p[1] - p[0] * p[0])],
        Bounds = (-2, 2, -1, 3),
        DefaultStart = [-1.2, 1],
        Minima = [[1, 1]],
    };

    /// <summary>f(x) = (x²−1)² + 0.25x. Two minima: a deeper one near x≈−1.03 and a shallower one near x≈0.97.</summary>
    public static GdFunction DoubleWell() => new()
    {
        Id = DoubleWellId,
        Title = "Две ямы (невыпуклая)",
        Formula = "f(x) = (x²−1)² + 0.25x",
        Dimension = 1,
        Value = p => Math.Pow(p[0] * p[0] - 1, 2) + 0.25 * p[0],
        Gradient = p => [4 * p[0] * (p[0] * p[0] - 1) + 0.25],
        Bounds = (-2, 2, 0, 0),
        DefaultStart = [0.5],
        Minima = [[-1.02989598505], [0.96714893788]],
    };

    public static GdFunction ById(string id, double conditioning = 10) => id switch
    {
        QuadraticId => Quadratic(conditioning),
        RosenbrockId => Rosenbrock(),
        DoubleWellId => DoubleWell(),
        _ => throw new ArgumentException($"Unknown gradient-descent function '{id}'.", nameof(id)),
    };
}

/// <summary>A stateful update rule: given the parameters and the gradient there, returns the next parameters.</summary>
public interface IGdOptimizer
{
    double[] Step(double[] parameters, double[] gradient);
}

/// <summary>x ← x − lr·g.</summary>
public sealed class PlainGradientDescent(double learningRate) : IGdOptimizer
{
    public double[] Step(double[] p, double[] g)
    {
        var next = new double[p.Length];
        for (int i = 0; i < p.Length; i++) next[i] = p[i] - learningRate * g[i];
        return next;
    }
}

/// <summary>Heavy-ball momentum in the PyTorch convention: v ← β·v + g;  x ← x − lr·v.</summary>
public sealed class MomentumOptimizer(double learningRate, double beta = 0.9) : IGdOptimizer
{
    private double[]? _velocity;

    public double[] Step(double[] p, double[] g)
    {
        _velocity ??= new double[p.Length];
        var next = new double[p.Length];
        for (int i = 0; i < p.Length; i++)
        {
            _velocity[i] = beta * _velocity[i] + g[i];
            next[i] = p[i] - learningRate * _velocity[i];
        }
        return next;
    }
}

/// <summary>
/// Adam (Kingma &amp; Ba): m ← β1·m + (1−β1)g;  v ← β2·v + (1−β2)g²;  m̂ = m/(1−β1ᵗ);  v̂ = v/(1−β2ᵗ);  x ← x − lr·m̂/(√v̂ + ε).
/// </summary>
public sealed class AdamOptimizer(double learningRate, double beta1 = 0.9, double beta2 = 0.999, double epsilon = 1e-8) : IGdOptimizer
{
    private double[]? _m;
    private double[]? _v;
    private int _t;

    public double[] Step(double[] p, double[] g)
    {
        _m ??= new double[p.Length];
        _v ??= new double[p.Length];
        _t++;
        double c1 = 1 - Math.Pow(beta1, _t);
        double c2 = 1 - Math.Pow(beta2, _t);
        var next = new double[p.Length];
        for (int i = 0; i < p.Length; i++)
        {
            _m[i] = beta1 * _m[i] + (1 - beta1) * g[i];
            _v[i] = beta2 * _v[i] + (1 - beta2) * g[i] * g[i];
            next[i] = p[i] - learningRate * (_m[i] / c1) / (Math.Sqrt(_v[i] / c2) + epsilon);
        }
        return next;
    }
}

/// <summary>One recorded point: step 0 is the start; <see cref="Loss"/> and <see cref="GradientNorm"/> are measured at <see cref="Parameters"/>.</summary>
public sealed record GdStep(int Step, double[] Parameters, double Loss, double GradientNorm);

public sealed record GdTrace(GdFunction Function, GdOptimizerKind Optimizer, IReadOnlyList<GdStep> Steps, GdStopReason StopReason)
{
    public GdStep Last => Steps[^1];
    public bool Diverged => StopReason == GdStopReason.Diverged;
}

public sealed record GdSettings
{
    public required GdFunction Function { get; init; }
    public GdOptimizerKind Optimizer { get; init; } = GdOptimizerKind.Gd;
    public double LearningRate { get; init; } = 0.1;
    /// <summary>Null = the function's default start.</summary>
    public IReadOnlyList<double>? Start { get; init; }
    public int MaxSteps { get; init; } = 200;
    public double GradientTolerance { get; init; } = 1e-6;
    public double Momentum { get; init; } = 0.9;
    /// <summary>The run is declared diverged when loss exceeds this factor times max(1, |initial loss|).</summary>
    public double LossExplosionFactor { get; init; } = 1e6;
}

public static class GradientDescentLab
{
    public const double DefaultLearningRate = 0.1;

    /// <summary>
    /// Runs the chosen optimiser and records every step. Stops on <see cref="GdSettings.MaxSteps"/>, on a gradient norm at or below the tolerance
    /// (converged), or when the loss/parameters go NaN/Infinity or the loss explodes (diverged). The stopping point is always the last trace entry.
    /// </summary>
    public static GdTrace Run(GdSettings s)
    {
        if (!double.IsFinite(s.LearningRate) || s.LearningRate <= 0) throw new ArgumentOutOfRangeException(nameof(s), "Learning rate must be positive and finite.");
        if (s.MaxSteps < 0) throw new ArgumentOutOfRangeException(nameof(s), "MaxSteps must be >= 0.");

        GdFunction f = s.Function;
        double[] p = s.Start is { } st ? st.ToArray() : (double[])f.DefaultStart.Clone();
        if (p.Length != f.Dimension) throw new ArgumentException($"Start must have {f.Dimension} coordinate(s).", nameof(s));

        IGdOptimizer opt = s.Optimizer switch
        {
            GdOptimizerKind.Gd => new PlainGradientDescent(s.LearningRate),
            GdOptimizerKind.Momentum => new MomentumOptimizer(s.LearningRate, s.Momentum),
            GdOptimizerKind.Adam => new AdamOptimizer(s.LearningRate),
            _ => throw new ArgumentOutOfRangeException(nameof(s)),
        };

        var steps = new List<GdStep>(Math.Min(s.MaxSteps, 10_000) + 1);
        double? initialLoss = null;
        for (int k = 0; ; k++)
        {
            double loss = f.Value(p);
            double[] g = f.Gradient(p);
            double norm = Norm(g);
            steps.Add(new GdStep(k, p, loss, norm));

            if (!double.IsFinite(loss) || !double.IsFinite(norm) || p.Any(v => !double.IsFinite(v)))
                return new GdTrace(f, s.Optimizer, steps, GdStopReason.Diverged);
            initialLoss ??= loss;
            if (loss > s.LossExplosionFactor * Math.Max(1, Math.Abs(initialLoss.Value)))
                return new GdTrace(f, s.Optimizer, steps, GdStopReason.Diverged);
            if (norm <= s.GradientTolerance)
                return new GdTrace(f, s.Optimizer, steps, GdStopReason.Converged);
            if (k >= s.MaxSteps)
                return new GdTrace(f, s.Optimizer, steps, GdStopReason.MaxSteps);

            p = opt.Step(p, g);
        }
    }

    public static double Norm(double[] v)
    {
        double sum = 0;
        foreach (double x in v) sum += x * x;
        return Math.Sqrt(sum);
    }

    /// <summary>Largest stable step for plain GD on a function with a global gradient Lipschitz constant L: 2/L.</summary>
    public static double? StabilityLimit(GdFunction f) => f.Lipschitz is { } l ? 2 / l : null;

    public static bool TryParseOptimizer(string? id, out GdOptimizerKind kind)
    {
        switch (id)
        {
            case "gd": kind = GdOptimizerKind.Gd; return true;
            case "momentum": kind = GdOptimizerKind.Momentum; return true;
            case "adam": kind = GdOptimizerKind.Adam; return true;
            default: kind = GdOptimizerKind.Gd; return false;
        }
    }

    public static string OptimizerId(GdOptimizerKind kind) => kind switch
    {
        GdOptimizerKind.Gd => "gd",
        GdOptimizerKind.Momentum => "momentum",
        _ => "adam",
    };
}

/// <summary>Starting learning rates that behave well for each function/optimiser pair (the lab's initial value; the user may change it).</summary>
public static class GdDefaults
{
    public static double LearningRate(string functionId, GdOptimizerKind optimizer) => (functionId, optimizer) switch
    {
        (GdFunctions.QuadraticId, GdOptimizerKind.Gd) => 0.1,
        (GdFunctions.QuadraticId, GdOptimizerKind.Momentum) => 0.05,
        (GdFunctions.QuadraticId, GdOptimizerKind.Adam) => 0.2,
        (GdFunctions.RosenbrockId, GdOptimizerKind.Gd) => 0.001,
        (GdFunctions.RosenbrockId, GdOptimizerKind.Momentum) => 0.0005,
        (GdFunctions.RosenbrockId, GdOptimizerKind.Adam) => 0.02,
        (GdFunctions.DoubleWellId, GdOptimizerKind.Gd) => 0.05,
        (GdFunctions.DoubleWellId, GdOptimizerKind.Momentum) => 0.02,
        (GdFunctions.DoubleWellId, GdOptimizerKind.Adam) => 0.05,
        _ => GradientDescentLab.DefaultLearningRate,
    };

    public static int DefaultSteps(string functionId) => functionId == GdFunctions.RosenbrockId ? 600 : 150;
}
