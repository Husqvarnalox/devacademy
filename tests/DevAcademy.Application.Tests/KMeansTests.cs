using DevAcademy.Application.Visualization;

namespace DevAcademy.Application.Tests;

public class KMeansTests
{
    private static readonly IReadOnlyList<(double X, double Y)> Blobs = PointData.DefaultBlobs();

    [Fact]
    public void A_fixed_seed_gives_the_same_trace()
    {
        foreach (var init in Enum.GetValues<KMeansInit>())
        {
            KMeansTrace a = KMeansLab.Run(Blobs, 3, init, seed: 5);
            KMeansTrace b = KMeansLab.Run(Blobs, 3, init, seed: 5);
            Assert.Equal(a.Steps.Count, b.Steps.Count);
            for (int i = 0; i < a.Steps.Count; i++)
            {
                Assert.Equal(a.Steps[i].Assignments, b.Steps[i].Assignments);
                Assert.Equal(a.Steps[i].Centroids, b.Steps[i].Centroids);
                Assert.Equal(a.Steps[i].Inertia, b.Steps[i].Inertia);
            }
        }
    }

    [Fact]
    public void Frames_alternate_assignment_and_update_and_inertia_never_increases()
    {
        foreach (int seed in new[] { 1, 2, 3, 4 })
            foreach (var init in Enum.GetValues<KMeansInit>())
            {
                KMeansTrace t = KMeansLab.Run(Blobs, 4, init, seed);
                Assert.Equal(KMeansPhase.Init, t.Steps[0].Phase);
                Assert.Null(t.Steps[0].Inertia);
                for (int i = 1; i < t.Steps.Count; i++)
                {
                    KMeansPhase expected = i % 2 == 1 ? KMeansPhase.Assign : KMeansPhase.Update;
                    Assert.Equal(expected, t.Steps[i].Phase);
                    if (i >= 2) Assert.True(t.Steps[i].Inertia <= t.Steps[i - 1].Inertia + 1e-9, $"seed {seed} frame {i}");
                }
            }
    }

    [Fact]
    public void Separable_blobs_converge_to_the_three_groups()
    {
        KMeansTrace t = KMeansLab.Run(Blobs, 3, KMeansInit.KMeansPlusPlus, seed: 1);
        Assert.True(t.Converged);
        Assert.InRange(t.Iterations, 0, KMeansLab.MaxIterations);
        Assert.Equal(0, t.Last.Changed);
        Assert.Equal(KMeansPhase.Assign, t.Last.Phase);
        // The blobs are generated 20 points at a time: each must land in a single cluster, and the three clusters must differ.
        var groups = Enumerable.Range(0, 3).Select(g => t.Last.Assignments.Skip(g * 20).Take(20).Distinct().ToList()).ToList();
        Assert.All(groups, g => Assert.Single(g));
        Assert.Equal(3, groups.Select(g => g[0]).Distinct().Count());
    }

    [Fact]
    public void The_last_frame_is_a_fixed_point_of_the_algorithm()
    {
        KMeansTrace t = KMeansLab.Run(Blobs, 3, KMeansInit.Random, seed: 2);
        KMeansStep update = t.Steps[^2];
        Assert.Equal(KMeansPhase.Update, update.Phase);
        Assert.Equal(update.Centroids, t.Last.Centroids);
        Assert.Equal(update.Assignments, t.Last.Assignments);
    }

    [Fact]
    public void The_iteration_limit_stops_a_run_that_has_not_converged()
    {
        KMeansTrace t = KMeansLab.Run(Blobs, 6, KMeansInit.Random, seed: 1, maxIterations: 1);
        Assert.False(t.Converged);
        Assert.Equal(1, t.Iterations);
        Assert.Equal(3, t.Steps.Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(99)]
    public void KMeansPlusPlus_chooses_k_distinct_points(int seed)
    {
        foreach (int k in new[] { 2, 3, 6 })
        {
            int[] idx = KMeansLab.InitPlusPlus(Blobs, k, new Random(seed));
            Assert.Equal(k, idx.Length);
            Assert.Equal(k, idx.Distinct().Count());
            Assert.Equal(k, idx.Select(i => Blobs[i]).Distinct().Count());
        }
    }

    [Fact]
    public void KMeansPlusPlus_spreads_centroids_over_well_separated_blobs()
    {
        // D² sampling makes it likely (about 90%, against 23% for uniform picks) that three far-apart blobs each get a centre.
        int hits = 0;
        for (int seed = 0; seed < 50; seed++)
            if (KMeansLab.InitPlusPlus(Blobs, 3, new Random(seed)).Select(i => i / 20).Distinct().Count() == 3) hits++;
        Assert.True(hits >= 35, $"only {hits}/50 seeds covered all blobs");
    }

    [Fact]
    public void KMeansPlusPlus_with_duplicate_points_still_returns_distinct_indices()
    {
        var same = Enumerable.Repeat((1.0, 1.0), 5).ToList();
        Assert.Equal(4, KMeansLab.InitPlusPlus(same, 4, new Random(1)).Distinct().Count());
    }

    [Fact]
    public void More_clusters_than_points_is_a_clear_error()
    {
        var ex = Assert.Throws<ArgumentException>(() => KMeansLab.Run([(0, 0), (1, 1), (2, 2)], 4));
        Assert.Contains("k must not exceed the number of points", ex.Message);
        Assert.Throws<ArgumentOutOfRangeException>(() => KMeansLab.Run(Blobs, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => KMeansLab.Run(Blobs, KMeansLab.MaxK + 1));
    }

    [Fact]
    public void Empty_clusters_keep_their_centroid()
    {
        // Two coincident points and k = 2: both centroids start on the same spot, so cluster 1 stays empty.
        KMeansTrace t = KMeansLab.Run([(0, 0), (0, 0), (0, 0)], 2, KMeansInit.Random, seed: 1);
        Assert.True(t.Converged);
        Assert.All(t.Last.Assignments, a => Assert.Equal(0, a));
        Assert.Equal(0, t.Last.Inertia);
    }

    [Fact]
    public void Point_parsing_accepts_xy_lines_and_rejects_garbage()
    {
        Assert.True(PointData.TryParse("1 2\n\n 3.5   -4 \n", out var pts, out _));
        Assert.Equal([(1.0, 2.0), (3.5, -4.0)], pts);
        Assert.True(PointData.TryParse("", out var none, out _));
        Assert.Empty(none);
        Assert.False(PointData.TryParse("1 2\n3", out _, out string? e1));
        Assert.Contains("line 2", e1);
        Assert.False(PointData.TryParse("1 NaN", out _, out _));
        Assert.False(PointData.TryParse("1 2 3", out _, out _));
        Assert.False(PointData.TryParse(string.Join('\n', Enumerable.Repeat("1 1", PointData.MaxPoints + 1)), out _, out _));
    }

    [Fact]
    public void Init_ids_round_trip()
    {
        foreach (var i in Enum.GetValues<KMeansInit>())
        {
            Assert.True(KMeansLab.TryParseInit(KMeansLab.InitId(i), out var back));
            Assert.Equal(i, back);
        }
        Assert.False(KMeansLab.TryParseInit("kmeans", out _));
    }
}

public class PcaTests
{
    private const double Tol = 1e-9;

    [Fact]
    public void Points_on_a_line_give_the_known_eigenpair()
    {
        // y = 2x: covariance [[2.5, 5], [5, 10]] has eigenvalues 12.5 and 0, eigenvector (1, 2)/√5.
        PcaResult r = PcaLab.Analyse([(-2, -4), (-1, -2), (0, 0), (1, 2), (2, 4)]);
        Assert.Equal(2.5, r.CovXx, 12);
        Assert.Equal(5, r.CovXy, 12);
        Assert.Equal(10, r.CovYy, 12);
        Assert.Equal(12.5, r.Eigenvalue1, 9);
        Assert.Equal(0, r.Eigenvalue2, 9);
        Assert.Equal(1 / Math.Sqrt(5), r.Pc1.X, 9);
        Assert.Equal(2 / Math.Sqrt(5), r.Pc1.Y, 9);
        Assert.Equal(1, r.Ratio1, 9);
        Assert.Equal(0, r.ReconstructionError, 9);
    }

    [Fact]
    public void Axis_aligned_data_has_the_coordinate_axes_as_components()
    {
        PcaResult r = PcaLab.Analyse([(1, 0), (-1, 0), (0, 3), (0, -3)]);
        Assert.Equal(6, r.Eigenvalue1, 9);          // 18 / 3
        Assert.Equal(2.0 / 3, r.Eigenvalue2, 9);    // 2 / 3
        Assert.Equal((0, 1), (Math.Abs(r.Pc1.X), Math.Abs(r.Pc1.Y)));
        Assert.Equal(1, r.Pc1.Y);
    }

    [Fact]
    public void Eigenpairs_satisfy_the_eigen_equation_and_vectors_are_orthonormal()
    {
        PcaResult r = PcaLab.Analyse(PointData.DefaultCorrelated());
        foreach (var (l, v) in new[] { (r.Eigenvalue1, r.Pc1), (r.Eigenvalue2, r.Pc2) })
        {
            Assert.Equal(l * v.X, r.CovXx * v.X + r.CovXy * v.Y, 8);
            Assert.Equal(l * v.Y, r.CovXy * v.X + r.CovYy * v.Y, 8);
            Assert.Equal(1, v.X * v.X + v.Y * v.Y, 9);
        }
        Assert.Equal(0, r.Pc1.X * r.Pc2.X + r.Pc1.Y * r.Pc2.Y, 9);
        Assert.True(r.Eigenvalue1 >= r.Eigenvalue2);
        Assert.Equal(r.CovXx + r.CovYy, r.Eigenvalue1 + r.Eigenvalue2, 9);
    }

    [Fact]
    public void Explained_variance_ratios_sum_to_one()
    {
        PcaResult r = PcaLab.Analyse(PointData.DefaultCorrelated());
        Assert.Equal(1, r.Ratio1 + r.Ratio2, Tol);
        Assert.InRange(r.Ratio1, 0.5, 1);
        Assert.True(r.Ratio1 > 0.9);
    }

    [Fact]
    public void Reconstruction_error_equals_the_discarded_variance()
    {
        var pts = PointData.DefaultCorrelated();
        PcaResult r = PcaLab.Analyse(pts);
        Assert.Equal((pts.Count - 1.0) / pts.Count * r.Eigenvalue2, r.ReconstructionError, 9);
    }

    [Fact]
    public void The_sign_convention_does_not_depend_on_point_order_or_mirroring()
    {
        var pts = PointData.DefaultCorrelated();
        PcaResult a = PcaLab.Analyse(pts);
        PcaResult b = PcaLab.Analyse(pts.Reverse().ToList());
        Assert.Equal(a.Pc1.X, b.Pc1.X, 9);
        Assert.Equal(a.Pc1.Y, b.Pc1.Y, 9);
        // Negating all coordinates flips the cloud but not the axes' orientation convention.
        PcaResult c = PcaLab.Analyse(pts.Select(p => (-p.X, -p.Y)).ToList());
        Assert.Equal(a.Pc1.X, c.Pc1.X, 9);
        Assert.Equal(a.Pc1.Y, c.Pc1.Y, 9);
        Assert.True(Math.Abs(a.Pc1.X) >= Math.Abs(a.Pc1.Y) ? a.Pc1.X > 0 : a.Pc1.Y > 0);
    }

    [Fact]
    public void A_diagonal_tie_resolves_to_a_positive_x()
    {
        PcaResult r = PcaLab.Analyse([(1, -1), (-1, 1), (0.5, -0.5), (-0.5, 0.5)]);
        Assert.Equal(1 / Math.Sqrt(2), r.Pc1.X, 9);
        Assert.Equal(-1 / Math.Sqrt(2), r.Pc1.Y, 9);
    }

    [Fact]
    public void Identical_points_are_degenerate_without_throwing()
    {
        PcaResult r = PcaLab.Analyse([(3, 4), (3, 4), (3, 4)]);
        Assert.True(r.IsDegenerate);
        Assert.Equal(0, r.Ratio1);
        Assert.Equal(0, r.Ratio2);
        Assert.Equal((1, 0), r.Pc1);
        Assert.All(r.Scores, s => Assert.Equal(0, s));
        Assert.Equal(5, PcaLab.Run([(3, 4), (3, 4), (3, 4)]).Steps.Count);
    }

    [Fact]
    public void Fewer_than_two_points_is_a_clear_error()
    {
        Assert.Throws<ArgumentException>(() => PcaLab.Analyse([(1, 1)]));
    }

    [Fact]
    public void Run_produces_the_five_stages_in_order()
    {
        var pts = PointData.DefaultCorrelated();
        PcaTrace t = PcaLab.Run(pts);
        Assert.Equal([PcaStage.Raw, PcaStage.Centered, PcaStage.Axes, PcaStage.Projection, PcaStage.Reconstruction], t.Steps.Select(s => s.Stage));
        Assert.Equal(pts, t.Steps[0].Points);
        Assert.Equal(0, t.Steps[1].Points.Average(p => p.X), 9);
        Assert.NotNull(t.Steps[3].Feet);
        // Each foot lies on the PC1 line, and the reconstruction is the feet shifted back by the mean.
        foreach (var (f, c) in t.Steps[3].Feet!.Zip(t.Result.Centered))
        {
            Assert.Equal(0, f.X * t.Result.Pc1.Y - f.Y * t.Result.Pc1.X, 9);
            Assert.Equal(0, (c.X - f.X) * t.Result.Pc1.X + (c.Y - f.Y) * t.Result.Pc1.Y, 9);
        }
        for (int i = 0; i < pts.Count; i++)
            Assert.Equal(t.Steps[3].Feet![i].X + t.Result.Mean.X, t.Steps[4].Points[i].X, 9);
        Assert.Equal(pts, t.Steps[4].Ghost);
    }
}
