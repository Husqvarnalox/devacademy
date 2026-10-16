using System.Globalization;

namespace DevAcademy.Application.Visualization;

public enum SortAlgorithm { Bubble, Insertion, Selection, Merge, Quick }

public enum StepKind { Compare, Swap, Write, MarkSorted, Range, Pivot }

/// <summary>
/// Compare: I,J = indices compared. Swap: I,J = indices swapped. Write: array[I] = Value.
/// MarkSorted: element at I is in its final position. Range: active sub-range [I..J] inclusive.
/// Pivot: I = pivot index (quicksort).
/// </summary>
public readonly record struct SortStep(StepKind Kind, int I, int J = 0, int Value = 0);

public sealed record SortTrace(SortAlgorithm Algorithm, IReadOnlyList<int> Input, IReadOnlyList<SortStep> Steps)
{
    public int Comparisons => Steps.Count(s => s.Kind == StepKind.Compare);
    public int Swaps => Steps.Count(s => s.Kind == StepKind.Swap);
    public int Writes => Steps.Count(s => s.Kind == StepKind.Write);
}

public static class SortTraceGenerator
{
    public static SortTrace Generate(SortAlgorithm algorithm, IReadOnlyList<int> input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var a = input.ToArray();
        var steps = new List<SortStep>();
        switch (algorithm)
        {
            case SortAlgorithm.Bubble: Bubble(a, steps); break;
            case SortAlgorithm.Insertion: Insertion(a, steps); break;
            case SortAlgorithm.Selection: Selection(a, steps); break;
            case SortAlgorithm.Merge: MergeSort(a, 0, a.Length - 1, new int[a.Length], steps); break;
            case SortAlgorithm.Quick: Quick(a, 0, a.Length - 1, steps); break;
            default: throw new ArgumentOutOfRangeException(nameof(algorithm));
        }

        var marked = new bool[a.Length];
        foreach (var s in steps)
            if (s.Kind == StepKind.MarkSorted) marked[s.I] = true;
        for (var i = 0; i < a.Length; i++)
            if (!marked[i]) steps.Add(new SortStep(StepKind.MarkSorted, i));

        return new SortTrace(algorithm, input.ToArray(), steps);
    }

    private static void Swap(int[] a, int i, int j, List<SortStep> steps)
    {
        (a[i], a[j]) = (a[j], a[i]);
        steps.Add(new SortStep(StepKind.Swap, i, j));
    }

    private static void Bubble(int[] a, List<SortStep> steps)
    {
        for (var pass = 0; pass < a.Length - 1; pass++)
        {
            var swapped = false;
            for (var j = 0; j < a.Length - 1 - pass; j++)
            {
                steps.Add(new SortStep(StepKind.Compare, j, j + 1));
                if (a[j] > a[j + 1]) { Swap(a, j, j + 1, steps); swapped = true; }
            }
            steps.Add(new SortStep(StepKind.MarkSorted, a.Length - 1 - pass));
            if (!swapped) break;
        }
    }

    private static void Insertion(int[] a, List<SortStep> steps)
    {
        for (var i = 1; i < a.Length; i++)
        {
            var key = a[i];
            var j = i - 1;
            while (j >= 0)
            {
                // The key is conceptually held in the hole at j + 1.
                steps.Add(new SortStep(StepKind.Compare, j, j + 1));
                if (a[j] <= key) break;
                a[j + 1] = a[j];
                steps.Add(new SortStep(StepKind.Write, j + 1, Value: a[j]));
                j--;
            }
            a[j + 1] = key;
            steps.Add(new SortStep(StepKind.Write, j + 1, Value: key));
        }
    }

    private static void Selection(int[] a, List<SortStep> steps)
    {
        for (var i = 0; i < a.Length - 1; i++)
        {
            var min = i;
            for (var j = i + 1; j < a.Length; j++)
            {
                steps.Add(new SortStep(StepKind.Compare, j, min));
                if (a[j] < a[min]) min = j;
            }
            if (min != i) Swap(a, i, min, steps);
            steps.Add(new SortStep(StepKind.MarkSorted, i));
        }
    }

    private static void MergeSort(int[] a, int lo, int hi, int[] tmp, List<SortStep> steps)
    {
        if (lo >= hi) return;
        var mid = lo + (hi - lo) / 2;
        MergeSort(a, lo, mid, tmp, steps);
        MergeSort(a, mid + 1, hi, tmp, steps);
        steps.Add(new SortStep(StepKind.Range, lo, hi));

        Array.Copy(a, lo, tmp, lo, hi - lo + 1);
        int l = lo, r = mid + 1;
        for (var k = lo; k <= hi; k++)
        {
            int v;
            if (l > mid) v = tmp[r++];
            else if (r > hi) v = tmp[l++];
            else
            {
                steps.Add(new SortStep(StepKind.Compare, l, r));
                v = tmp[l] <= tmp[r] ? tmp[l++] : tmp[r++];
            }
            a[k] = v;
            steps.Add(new SortStep(StepKind.Write, k, Value: v));
        }
    }

    private static void Quick(int[] a, int lo, int hi, List<SortStep> steps)
    {
        if (lo > hi) return;
        if (lo == hi) { steps.Add(new SortStep(StepKind.MarkSorted, lo)); return; }
        steps.Add(new SortStep(StepKind.Range, lo, hi));
        steps.Add(new SortStep(StepKind.Pivot, hi));
        var pivot = a[hi];
        var i = lo;
        for (var j = lo; j < hi; j++)
        {
            steps.Add(new SortStep(StepKind.Compare, j, hi));
            if (a[j] < pivot)
            {
                if (i != j) Swap(a, i, j, steps);
                i++;
            }
        }
        if (i != hi) Swap(a, i, hi, steps);
        steps.Add(new SortStep(StepKind.MarkSorted, i));
        Quick(a, lo, i - 1, steps);
        Quick(a, i + 1, hi, steps);
    }
}

public static class SortTraceReplayer
{
    /// <summary>Array state after applying the first <paramref name="count"/> steps.</summary>
    public static int[] StateAt(SortTrace trace, int count)
    {
        ArgumentNullException.ThrowIfNull(trace);
        var a = trace.Input.ToArray();
        var n = Math.Clamp(count, 0, trace.Steps.Count);
        for (var k = 0; k < n; k++)
        {
            var s = trace.Steps[k];
            if (s.Kind == StepKind.Swap) (a[s.I], a[s.J]) = (a[s.J], a[s.I]);
            else if (s.Kind == StepKind.Write) a[s.I] = s.Value;
        }
        return a;
    }
}

public static class SortInput
{
    public const int MinLength = 2, MaxLength = 40, MinValue = 1, MaxValue = 99;

    public static bool TryParse(string? text, out IReadOnlyList<int> values, out string? errorKey)
    {
        values = [];
        var tokens = (text ?? "").Split([',', ' ', ';', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) { errorKey = "Viz_ErrEmpty"; return false; }
        var list = new List<int>(tokens.Length);
        foreach (var t in tokens)
        {
            if (!int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            {
                errorKey = "Viz_ErrNotInt";
                return false;
            }
            list.Add(v);
        }
        if (list.Count < MinLength || list.Count > MaxLength) { errorKey = "Viz_ErrLength"; return false; }
        if (list.Any(v => v < MinValue || v > MaxValue)) { errorKey = "Viz_ErrRange"; return false; }
        values = list;
        errorKey = null;
        return true;
    }

    public static IReadOnlyList<int> Random(Random rng, int length)
    {
        ArgumentNullException.ThrowIfNull(rng);
        var n = Math.Clamp(length, MinLength, MaxLength);
        var r = new int[n];
        for (var i = 0; i < n; i++) r[i] = rng.Next(MinValue, MaxValue + 1);
        return r;
    }
}
