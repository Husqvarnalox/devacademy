using DevAcademy.Application.Python;
using DevAcademy.Application.Visualization;
using DevAcademy.Domain;
using DevAcademy.Execution;

namespace DevAcademy.Application.Tests;

public class GradientDescentTests
{
    private static GdTrace Run(GdFunction f, GdOptimizerKind o, double lr, int steps = 500, double[]? start = null, double tol = 1e-8) =>
        GradientDescentLab.Run(new GdSettings { Function = f, Optimizer = o, LearningRate = lr, MaxSteps = steps, Start = start, GradientTolerance = tol });

    [Theory]
    [InlineData(GdOptimizerKind.Gd, 0.1)]
    [InlineData(GdOptimizerKind.Momentum, 0.02)]
    [InlineData(GdOptimizerKind.Adam, 0.1)]
    public void Quadratic_converges_to_the_analytic_minimum(GdOptimizerKind kind, double lr)
    {
        GdTrace t = Run(GdFunctions.Quadratic(10), kind, lr, steps: 3000, tol: 1e-6);
        Assert.NotEqual(GdStopReason.Diverged, t.StopReason);
        Assert.InRange(Math.Abs(t.Last.Parameters[0]), 0, 1e-3);
        Assert.InRange(Math.Abs(t.Last.Parameters[1]), 0, 1e-3);
    }

    [Fact]
    public void Plain_gd_on_quadratic_matches_the_closed_form()
    {
        // x_k = (1-lr)^k x0 ; y_k = (1-lr*kappa)^k y0.
        GdTrace t = Run(GdFunctions.Quadratic(4), GdOptimizerKind.Gd, 0.2, steps: 10, start: [2, 1], tol: 0);
        GdStep s = t.Steps[10];
        Assert.Equal(2 * Math.Pow(0.8, 10), s.Parameters[0], 12);
        Assert.Equal(Math.Pow(1 - 0.2 * 4, 10), s.Parameters[1], 12);
    }

    [Fact]
    public void Rosenbrock_and_double_well_reach_their_minima()
    {
        GdTrace r = Run(GdFunctions.Rosenbrock(), GdOptimizerKind.Adam, 0.02, steps: 20000, start: [-1.2, 1], tol: 1e-6);
        Assert.InRange(r.Last.Loss, 0, 1e-3);
        Assert.Equal(1, r.Last.Parameters[0], 1);

        GdTrace d = Run(GdFunctions.DoubleWell(), GdOptimizerKind.Gd, 0.05, steps: 5000, start: [-0.5], tol: 1e-10);
        Assert.Equal(GdStopReason.Converged, d.StopReason);
        Assert.Equal(-1.02989598505, d.Last.Parameters[0], 6);
        GdTrace d2 = Run(GdFunctions.DoubleWell(), GdOptimizerKind.Gd, 0.05, steps: 5000, start: [0.5], tol: 1e-10);
        Assert.Equal(0.96714893788, d2.Last.Parameters[0], 6);
    }

    [Fact]
    public void Learning_rate_above_two_over_L_diverges_and_below_it_does_not()
    {
        GdFunction f = GdFunctions.Quadratic(10);
        double limit = GradientDescentLab.StabilityLimit(f)!.Value;
        Assert.Equal(0.2, limit, 12);
        GdTrace bad = Run(f, GdOptimizerKind.Gd, limit * 1.05, steps: 2000);
        Assert.Equal(GdStopReason.Diverged, bad.StopReason);
        GdTrace good = Run(f, GdOptimizerKind.Gd, limit * 0.95, steps: 2000, tol: 1e-6);
        Assert.NotEqual(GdStopReason.Diverged, good.StopReason);
        Assert.True(good.Last.Loss < bad.Steps[0].Loss);
    }

    [Fact]
    public void Non_finite_values_stop_the_run_as_diverged()
    {
        GdTrace t = Run(GdFunctions.Rosenbrock(), GdOptimizerKind.Gd, 1.0, steps: 1000);
        Assert.Equal(GdStopReason.Diverged, t.StopReason);
        Assert.True(t.Steps.Count < 1000);
    }

    [Fact]
    public void Max_steps_stop_records_exactly_max_steps_plus_the_start()
    {
        GdTrace t = Run(GdFunctions.Rosenbrock(), GdOptimizerKind.Gd, 0.0005, steps: 7, tol: 0);
        Assert.Equal(GdStopReason.MaxSteps, t.StopReason);
        Assert.Equal(8, t.Steps.Count);
        Assert.Equal(Enumerable.Range(0, 8), t.Steps.Select(s => s.Step));
    }

    [Fact]
    public void Momentum_matches_a_hand_computed_two_step_example()
    {
        // lr=0.1, beta=0.9, x0=1, g1=2, g2=1:  v1=2 -> x1=0.8 ; v2=0.9*2+1=2.8 -> x2=0.52
        var opt = new MomentumOptimizer(0.1, 0.9);
        double[] x1 = opt.Step([1], [2]);
        Assert.Equal(0.8, x1[0], 12);
        double[] x2 = opt.Step(x1, [1]);
        Assert.Equal(0.52, x2[0], 12);
    }

    [Fact]
    public void Adam_matches_a_hand_computed_two_step_example()
    {
        // lr=0.1, b1=0.9, b2=0.999, eps=1e-8, x0=1, g1=2, g2=1.
        // t=1: m=0.2 v=0.004 -> m^=2 v^=4 -> x1 = 1 - 0.1*2/(2+1e-8) = 0.9000000005
        // t=2: m=0.28 v=0.004995 -> m^=0.28/0.19 v^=0.004995/0.001999 -> x2 = 0.9 - 0.1*m^/(sqrt(v^)+1e-8) = 0.80678203721
        var opt = new AdamOptimizer(0.1);
        double[] x1 = opt.Step([1], [2]);
        Assert.Equal(0.9000000005, x1[0], 10);
        double[] x2 = opt.Step(x1, [1]);
        Assert.Equal(0.80678203721, x2[0], 9);
    }

    [Theory]
    [InlineData("quadratic")]
    [InlineData("rosenbrock")]
    [InlineData("double-well")]
    public void Analytic_gradients_match_central_finite_differences(string id)
    {
        GdFunction f = GdFunctions.ById(id, 7);
        var rng = new Random(42);
        for (int n = 0; n < 25; n++)
        {
            double[] p = new double[f.Dimension];
            for (int i = 0; i < p.Length; i++) p[i] = (rng.NextDouble() * 2 - 1) * 2;
            double[] g = f.Gradient(p);
            for (int i = 0; i < p.Length; i++)
            {
                const double h = 1e-6;
                double[] a = (double[])p.Clone(), b = (double[])p.Clone();
                a[i] += h; b[i] -= h;
                double numeric = (f.Value(a) - f.Value(b)) / (2 * h);
                Assert.True(Math.Abs(numeric - g[i]) <= 1e-5 * Math.Max(1, Math.Abs(g[i])), $"{id} d/dx{i} at [{string.Join(',', p)}]: {numeric} vs {g[i]}");
            }
        }
    }

    [Fact]
    public void Gradients_vanish_at_the_declared_minima()
    {
        foreach (string id in GdFunctions.Ids)
        {
            GdFunction f = GdFunctions.ById(id);
            foreach (double[] m in f.Minima) Assert.True(GradientDescentLab.Norm(f.Gradient(m)) < 1e-6, id);
        }
    }

    [Fact]
    public void Invalid_settings_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Run(GdFunctions.Quadratic(), GdOptimizerKind.Gd, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Run(GdFunctions.Quadratic(), GdOptimizerKind.Gd, double.NaN));
        Assert.Throws<ArgumentException>(() => Run(GdFunctions.Quadratic(), GdOptimizerKind.Gd, 0.1, start: [1]));
        Assert.Throws<ArgumentOutOfRangeException>(() => GdFunctions.Quadratic(0.5));
        Assert.Throws<ArgumentException>(() => GdFunctions.ById("nope"));
        Assert.True(GradientDescentLab.TryParseOptimizer("adam", out GdOptimizerKind k));
        Assert.Equal(GdOptimizerKind.Adam, k);
        Assert.False(GradientDescentLab.TryParseOptimizer("sgd?", out _));
    }
}

public class PythonRequirementTests
{
    private static PythonProbeResult Probe(string version, params (string, string)[] installed) =>
        new(true, null, version, "cpython", installed.ToDictionary(p => p.Item1, p => p.Item2));

    [Fact]
    public void Satisfied_when_python_and_packages_match()
    {
        var req = new ExerciseRequirements(">=3.11", ["numpy>=2", "pandas"]);
        RequirementCheckResult r = RequirementChecker.Check(req, Probe("3.12.4", ("numpy", "2.1.0"), ("pandas", "2.2.0")));
        Assert.True(r.Satisfied);
    }

    [Fact]
    public void Missing_package_old_package_and_old_python_are_each_reported()
    {
        var req = new ExerciseRequirements(">=3.11", ["numpy>=2", "scipy"]);
        RequirementCheckResult r = RequirementChecker.Check(req, Probe("3.10.1", ("numpy", "1.26.4")));
        Assert.False(r.Satisfied);
        Assert.Equal(3, r.Missing.Count);
        Assert.Contains(r.Missing, m => m.Requirement == "scipy" && m.Reason == "не установлен");
        Assert.Contains(r.Missing, m => m.Requirement == "numpy>=2" && m.Reason.Contains("1.26.4"));
        Assert.Equal(["numpy>=2", "scipy"], r.MissingPackages.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void A_failed_probe_is_not_satisfied_and_is_not_treated_as_missing_packages()
    {
        RequirementCheckResult r = RequirementChecker.Check(new ExerciseRequirements(null, ["numpy"]), new PythonProbeResult(false, "boom", null, null, new Dictionary<string, string>()));
        Assert.False(r.Satisfied);
        Assert.Equal("boom", r.ProbeError);
        Assert.Empty(r.Missing);
    }

    [Fact]
    public void Accelerator_assessment_prefers_frameworks_and_explains_failures()
    {
        var hw = new HardwareReport("Linux", "x64", "c", 4, 0, [], [new("nvidia", "NVIDIA", CapabilityStatus.Available, "RTX")]);
        var torchCpu = new AcceleratorCapability("torch.cuda", "torch", CapabilityStatus.NotAvailable, "GPU найден, но установленный torch собран без CUDA");
        AcceleratorAssessment a = AcceleratorAssessment.Assess(hw, [torchCpu, new("jax", "JAX", CapabilityStatus.NotInstalled, "x")]);
        Assert.False(a.Usable);
        Assert.Contains("собран без CUDA", a.Explanation);

        Assert.True(AcceleratorAssessment.Assess(hw, null).Usable);                     // nothing probed: hardware decides
        var none = new HardwareReport("Linux", "x64", "c", 4, 0, [], [new("nvidia", "NVIDIA", CapabilityStatus.NotAvailable, "нет")]);
        Assert.False(AcceleratorAssessment.Assess(none, null).Usable);
        var ok = new AcceleratorCapability("torch.mps", "torch", CapabilityStatus.Available, "mps");
        Assert.True(AcceleratorAssessment.Assess(none, [ok]).Usable);
    }
}
