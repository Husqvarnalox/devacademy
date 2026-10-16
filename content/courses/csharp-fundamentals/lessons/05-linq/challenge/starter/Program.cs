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
    // TODO: превратите операторы в ленивые итераторы (yield return),
    // чтобы строки журнала шли вперемешку, а Take(k) не просил лишний элемент.
    public static IEnumerable<int> Where(IEnumerable<int> source, Func<int, bool> predicate)
    {
        var result = new List<int>();
        foreach (int x in source)
        {
            bool keep = predicate(x);
            Console.WriteLine($"where {x}: {(keep ? "keep" : "skip")}");
            if (keep) result.Add(x);
        }
        return result;
    }

    public static IEnumerable<int> Select(IEnumerable<int> source, Func<int, int> selector)
    {
        var result = new List<int>();
        foreach (int x in source)
        {
            int y = selector(x);
            Console.WriteLine($"select {x} -> {y}");
            result.Add(y);
        }
        return result;
    }

    public static IEnumerable<int> Take(IEnumerable<int> source, int count) => source.ToList().GetRange(0, Math.Min(count, source.Count()));
}
