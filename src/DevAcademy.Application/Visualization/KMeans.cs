namespace DevAcademy.Application.Visualization;

public enum KMeansInit { Random, KMeansPlusPlus }

public enum KMeansPhase
{
    /// <summary>The chosen starting centroids; nothing is assigned yet.</summary>
    Init,
    /// <summary>Every point joined its nearest centroid.</summary>
    Assign,
    /// <summary>Every centroid moved to the mean of its points.</summary>
    Update,
}

/// <summary>
/// One recorded frame. <see cref="Assignments"/> are -1 in the <see cref="KMeansPhase.Init"/> frame; <see cref="Inertia"/> is the sum of squared
/// distances from the points to the centroids of their clusters, measured with this frame's centroids (null before the first assignment).
/// <see cref="Changed"/> is the number of points whose cluster changed in an assignment step.
/// </summary>
public sealed record KMeansStep(int Iteration, KMeansPhase Phase, int[] Assignments, (double X, double Y)[] Centroids, double? Inertia, int Changed);

/// <summary>
/// <see cref="Iterations"/> counts the update steps performed. When <see cref="Converged"/> the last frame is an assignment that changed nothing;
/// otherwise the run stopped at the iteration limit.
/// </summary>
public sealed record KMeansTrace(IReadOnlyList<(double X, double Y)> Points, int K, KMeansInit Init, int Seed, IReadOnlyList<KMeansStep> Steps, bool Converged, int Iterations)
{
    public KMeansStep Last => Steps[^1];
}

public static class KMeansLab
{
    public const int MinK = 2, MaxK = 6, DefaultSeed = 1, MaxIterations = 50;

    public static bool TryParseInit(string? id, out KMeansInit init)
    {
        switch (id)
        {
            case "random": init = KMeansInit.Random; return true;
            case "kmeans++": init = KMeansInit.KMeansPlusPlus; return true;
            default: init = KMeansInit.Random; return false;
        }
    }

    public static string InitId(KMeansInit init) => init == KMeansInit.Random ? "random" : "kmeans++";

    /// <summary>
    /// Lloyd's algorithm with alternating assignment and update frames. Ties go to the lower cluster index; an empty cluster keeps its centroid.
    /// Stops when an assignment step changes nothing, or after <paramref name="maxIterations"/> iterations. Deterministic for a given seed.
    /// </summary>
    public static KMeansTrace Run(IReadOnlyList<(double X, double Y)> points, int k, KMeansInit init = KMeansInit.KMeansPlusPlus, int seed = DefaultSeed, int maxIterations = MaxIterations)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (k < MinK || k > MaxK) throw new ArgumentOutOfRangeException(nameof(k), $"k must be between {MinK} and {MaxK}.");
        if (k > points.Count) throw new ArgumentException($"Cannot form {k} clusters from {points.Count} point(s): k must not exceed the number of points.", nameof(k));
        if (maxIterations < 1) throw new ArgumentOutOfRangeException(nameof(maxIterations));

        var rng = new Random(seed);
        var centroids = (init == KMeansInit.Random ? InitRandom(points, k, rng) : InitPlusPlus(points, k, rng)).Select(i => points[i]).ToArray();
        int n = points.Count;
        var assign = new int[n];
        Array.Fill(assign, -1);
        var steps = new List<KMeansStep> { new(0, KMeansPhase.Init, (int[])assign.Clone(), centroids.ToArray(), null, 0) };

        for (int it = 1; it <= maxIterations; it++)
        {
            int changed = 0;
            for (int i = 0; i < n; i++)
            {
                int best = Nearest(points[i], centroids);
                if (best != assign[i]) { assign[i] = best; changed++; }
            }
            steps.Add(new KMeansStep(it, KMeansPhase.Assign, (int[])assign.Clone(), centroids.ToArray(), Inertia(points, assign, centroids), changed));
            if (changed == 0) return new KMeansTrace(points, k, init, seed, steps, true, it - 1);

            var sums = new (double X, double Y, int N)[k];
            for (int i = 0; i < n; i++)
            {
                var s = sums[assign[i]];
                sums[assign[i]] = (s.X + points[i].X, s.Y + points[i].Y, s.N + 1);
            }
            for (int j = 0; j < k; j++)
                if (sums[j].N > 0) centroids[j] = (sums[j].X / sums[j].N, sums[j].Y / sums[j].N);
            steps.Add(new KMeansStep(it, KMeansPhase.Update, (int[])assign.Clone(), centroids.ToArray(), Inertia(points, assign, centroids), 0));
        }
        return new KMeansTrace(points, k, init, seed, steps, false, maxIterations);
    }

    public static double SquaredDistance((double X, double Y) a, (double X, double Y) b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }

    private static int Nearest((double X, double Y) p, (double X, double Y)[] centroids)
    {
        int best = 0;
        double bestD = SquaredDistance(p, centroids[0]);
        for (int j = 1; j < centroids.Length; j++)
        {
            double d = SquaredDistance(p, centroids[j]);
            if (d < bestD) { bestD = d; best = j; }
        }
        return best;
    }

    private static double Inertia(IReadOnlyList<(double X, double Y)> points, int[] assign, (double X, double Y)[] centroids)
    {
        double sum = 0;
        for (int i = 0; i < points.Count; i++) sum += SquaredDistance(points[i], centroids[assign[i]]);
        return sum;
    }

    /// <summary>k distinct point indices drawn uniformly without replacement (partial Fisher-Yates).</summary>
    public static int[] InitRandom(IReadOnlyList<(double X, double Y)> points, int k, Random rng)
    {
        var idx = Enumerable.Range(0, points.Count).ToArray();
        for (int i = 0; i < k; i++)
        {
            int j = i + rng.Next(idx.Length - i);
            (idx[i], idx[j]) = (idx[j], idx[i]);
        }
        return idx[..k];
    }

    /// <summary>
    /// k-means++ (Arthur &amp; Vassilvitskii, 2007): the first centre is uniform; each next one is a point drawn with probability proportional to D(x)²,
    /// the squared distance to its nearest chosen centre. Chosen points have D = 0 so are never drawn twice; if every remaining D² is 0 (duplicate
    /// coordinates) an unchosen point is taken uniformly. Returns k distinct point indices.
    /// </summary>
    public static int[] InitPlusPlus(IReadOnlyList<(double X, double Y)> points, int k, Random rng)
    {
        int n = points.Count;
        var chosen = new List<int> { rng.Next(n) };
        var d2 = new double[n];
        for (int i = 0; i < n; i++) d2[i] = SquaredDistance(points[i], points[chosen[0]]);
        while (chosen.Count < k)
        {
            double total = d2.Sum();
            int pick = -1;
            if (total > 0)
            {
                double target = rng.NextDouble() * total, acc = 0;
                for (int i = 0; i < n; i++)
                {
                    if (d2[i] <= 0) continue;
                    acc += d2[i];
                    pick = i;
                    if (acc > target) break;
                }
            }
            else
            {
                var free = Enumerable.Range(0, n).Where(i => !chosen.Contains(i)).ToList();
                pick = free[rng.Next(free.Count)];
            }
            chosen.Add(pick);
            for (int i = 0; i < n; i++) d2[i] = Math.Min(d2[i], SquaredDistance(points[i], points[pick]));
        }
        return chosen.ToArray();
    }
}
