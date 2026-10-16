#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

string[] lines = Console.In.ReadToEnd().Split('\n', StringSplitOptions.TrimEntries);
int[] numbers = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries)
    .Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToArray();

IEnumerable<int> query = numbers;
foreach (string stage in lines[1].Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
{
    string[] p = stage.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    query = p switch
    {
        ["where", "even"] => Lazy.Where(query, x => x % 2 == 0),
        ["where", "odd"] => Lazy.Where(query, x => x % 2 != 0),
        ["select", "sq"] => Lazy.Select(query, x => x * x),
        ["select", "inc"] => Lazy.Select(query, x => x + 1),
        ["take", var k] => Lazy.Take(query, int.Parse(k, CultureInfo.InvariantCulture)),
        _ => throw new ArgumentException($"bad stage: {stage}"),
    };
}

Console.WriteLine("built");
foreach (int y in query)
    Console.WriteLine($"got {y}");
Console.WriteLine("done");

static class Lazy
{
    public static IEnumerable<int> Where(IEnumerable<int> source, Func<int, bool> predicate)
    {
        foreach (int x in source)
        {
            bool keep = predicate(x);
            Console.WriteLine($"where {x}: {(keep ? "keep" : "skip")}");
            if (keep) yield return x;
        }
    }

    public static IEnumerable<int> Select(IEnumerable<int> source, Func<int, int> selector)
    {
        foreach (int x in source)
        {
            int y = selector(x);
            Console.WriteLine($"select {x} -> {y}");
            yield return y;
        }
    }

    public static IEnumerable<int> Take(IEnumerable<int> source, int count)
    {
        if (count <= 0) yield break;
        int taken = 0;
        foreach (int x in source)
        {
            yield return x;
            if (++taken >= count) yield break;
        }
    }
}
