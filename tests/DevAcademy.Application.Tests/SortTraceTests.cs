using DevAcademy.Application.Visualization;

namespace DevAcademy.Application.Tests;

public class SortTraceTests
{
    public static IEnumerable<object[]> Cases()
    {
        var rng = new Random(12345);
        var arrays = new List<int[]>
        {
            new[] { 1, 2, 3, 4, 5, 6 },
            new[] { 9, 8, 7, 6, 5, 4, 3 },
            new[] { 5, 3, 5, 1, 3, 1, 5 },
            new[] { 1, 2, 4, 3, 5, 6 },
            new[] { 7, 7, 7, 7 },
            new[] { 2, 1 },
            new[] { 1, 2 },
            SortInput.Random(rng, 12).ToArray(),
            SortInput.Random(rng, 40).ToArray(),
        };
        foreach (var alg in Enum.GetValues<SortAlgorithm>())
            foreach (var arr in arrays)
                yield return [alg, arr];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void TraceIsCorrect(SortAlgorithm alg, int[] input)
    {
        var trace = SortTraceGenerator.Generate(alg, input);
        var n = input.Length;

        var expected = input.ToArray();
        Array.Sort(expected);
        Assert.Equal(expected, SortTraceReplayer.StateAt(trace, trace.Steps.Count));
        Assert.Equal(input, SortTraceReplayer.StateAt(trace, 0));

        if (alg is SortAlgorithm.Bubble or SortAlgorithm.Selection or SortAlgorithm.Quick)
        {
            var sortedInput = input.OrderBy(x => x).ToArray();
            for (var k = 0; k <= trace.Steps.Count; k++)
                Assert.Equal(sortedInput, SortTraceReplayer.StateAt(trace, k).OrderBy(x => x).ToArray());
        }

        var (cmp, swp) = Reference(alg, input);
        Assert.Equal(cmp, trace.Comparisons);
        Assert.Equal(swp, trace.Swaps);

        var marked = new bool[n];
        foreach (var s in trace.Steps)
        {
            Assert.InRange(s.I, 0, n - 1);
            if (s.Kind is StepKind.Compare or StepKind.Swap or StepKind.Range) Assert.InRange(s.J, 0, n - 1);
            if (s.Kind == StepKind.MarkSorted) marked[s.I] = true;
        }
        Assert.All(marked, Assert.True);
    }

    [Fact]
    public void MergeHasNoSwapsAndInsertionHasNoSwaps()
    {
        var input = new[] { 5, 2, 9, 1, 7 };
        Assert.Equal(0, SortTraceGenerator.Generate(SortAlgorithm.Merge, input).Swaps);
        Assert.Equal(0, SortTraceGenerator.Generate(SortAlgorithm.Insertion, input).Swaps);
        Assert.True(SortTraceGenerator.Generate(SortAlgorithm.Merge, input).Writes > 0);
    }

    // ---- independent textbook reference implementations with counters ----
    private static (int cmp, int swp) Reference(SortAlgorithm alg, int[] input)
    {
        var a = (int[])input.Clone();
        int c = 0, s = 0;
        switch (alg)
        {
            case SortAlgorithm.Bubble:
                for (var end = a.Length - 1; end > 0; end--)
                {
                    var any = false;
                    for (var j = 0; j < end; j++)
                    {
                        c++;
                        if (a[j] > a[j + 1]) { (a[j], a[j + 1]) = (a[j + 1], a[j]); s++; any = true; }
                    }
                    if (!any) break;
                }
                break;
            case SortAlgorithm.Insertion:
                for (var i = 1; i < a.Length; i++)
                {
                    var key = a[i];
                    var j = i - 1;
                    while (j >= 0)
                    {
                        c++;
                        if (a[j] > key) { a[j + 1] = a[j]; j--; } else break;
                    }
                    a[j + 1] = key;
                }
                break;
            case SortAlgorithm.Selection:
                for (var i = 0; i < a.Length - 1; i++)
                {
                    var m = i;
                    for (var j = i + 1; j < a.Length; j++) { c++; if (a[j] < a[m]) m = j; }
                    if (m != i) { (a[i], a[m]) = (a[m], a[i]); s++; }
                }
                break;
            case SortAlgorithm.Merge:
                a = MergeRef(a, ref c);
                break;
            case SortAlgorithm.Quick:
                QuickRef(a, 0, a.Length - 1, ref c, ref s);
                break;
        }
        Assert.Equal(input.OrderBy(x => x).ToArray(), a);
        return (c, s);
    }

    private static int[] MergeRef(int[] x, ref int c)
    {
        if (x.Length <= 1) return x;
        var left = MergeRef(x[..((x.Length + 1) / 2)], ref c);
        var right = MergeRef(x[((x.Length + 1) / 2)..], ref c);
        var res = new List<int>();
        int p = 0, q = 0;
        while (p < left.Length && q < right.Length)
        {
            c++;
            if (left[p] <= right[q]) res.Add(left[p++]); else res.Add(right[q++]);
        }
        while (p < left.Length) res.Add(left[p++]);
        while (q < right.Length) res.Add(right[q++]);
        return [.. res];
    }

    private static void QuickRef(int[] a, int lo, int hi, ref int c, ref int s)
    {
        if (lo >= hi) return;
        var pv = a[hi];
        var i = lo - 1;
        for (var j = lo; j < hi; j++)
        {
            c++;
            if (a[j] < pv)
            {
                i++;
                if (i != j) { (a[i], a[j]) = (a[j], a[i]); s++; }
            }
        }
        if (i + 1 != hi) { (a[i + 1], a[hi]) = (a[hi], a[i + 1]); s++; }
        QuickRef(a, lo, i, ref c, ref s);
        QuickRef(a, i + 2, hi, ref c, ref s);
    }

    // ---- SortInput ----
    [Theory]
    [InlineData("5, 3 8;1\n9", 5)]
    [InlineData("1 99", 2)]
    public void ParseValid(string text, int count)
    {
        Assert.True(SortInput.TryParse(text, out var v, out var err));
        Assert.Null(err);
        Assert.Equal(count, v.Count);
    }

    [Theory]
    [InlineData(null, "Viz_ErrEmpty")]
    [InlineData("  ,; ", "Viz_ErrEmpty")]
    [InlineData("1, a, 3", "Viz_ErrNotInt")]
    [InlineData("1.5 2", "Viz_ErrNotInt")]
    [InlineData("5", "Viz_ErrLength")]
    [InlineData("0 5", "Viz_ErrRange")]
    [InlineData("5 100", "Viz_ErrRange")]
    public void ParseInvalid(string? text, string key)
    {
        Assert.False(SortInput.TryParse(text, out _, out var err));
        Assert.Equal(key, err);
    }

    [Fact]
    public void ParseLengthBounds()
    {
        Assert.False(SortInput.TryParse(string.Join(' ', Enumerable.Repeat(5, 41)), out _, out var err));
        Assert.Equal("Viz_ErrLength", err);
        Assert.True(SortInput.TryParse(string.Join(' ', Enumerable.Repeat(5, 40)), out _, out _));
    }

    [Fact]
    public void RandomWithinBounds()
    {
        var r = SortInput.Random(new Random(1), 40);
        Assert.Equal(40, r.Count);
        Assert.All(r, x => Assert.InRange(x, SortInput.MinValue, SortInput.MaxValue));
    }
}
