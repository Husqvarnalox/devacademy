#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

string[] lines = Console.In.ReadToEnd().Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
int n = int.Parse(lines[0], CultureInfo.InvariantCulture);

var points = new Dictionary<string, Point>();
var history = new Dictionary<string, Stack<Point>>();

for (int i = 1; i <= n; i++)
{
    string[] a = lines[i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
    switch (a[0])
    {
        case "add":
            if (points.ContainsKey(a[1])) { Console.WriteLine($"exists: {a[1]}"); break; }
            points[a[1]] = new Point(ParseInt(a[2]), ParseInt(a[3]));
            history[a[1]] = new Stack<Point>();
            break;
        case "move":
            if (!points.TryGetValue(a[1], out Point? current)) { Console.WriteLine($"missing: {a[1]}"); break; }
            history[a[1]].Push(current);
            // TODO: не меняйте объект на месте - создайте новую точку через with
            current.X += ParseInt(a[2]);
            current.Y += ParseInt(a[3]);
            break;
        case "undo":
            if (!points.ContainsKey(a[1])) { Console.WriteLine($"missing: {a[1]}"); break; }
            if (history[a[1]].Count == 0) Console.WriteLine("nothing to undo");
            else points[a[1]] = history[a[1]].Pop();
            break;
        case "show":
            if (points.TryGetValue(a[1], out Point? shown)) Console.WriteLine(shown);
            else Console.WriteLine($"missing: {a[1]}");
            break;
        case "same":
            if (!points.TryGetValue(a[1], out Point? left)) { Console.WriteLine($"missing: {a[1]}"); break; }
            if (!points.TryGetValue(a[2], out Point? right)) { Console.WriteLine($"missing: {a[2]}"); break; }
            Console.WriteLine(left == right);
            break;
        case "distinct":
            Console.WriteLine($"distinct={new HashSet<Point>(points.Values).Count}");
            break;
    }
}

static int ParseInt(string s) => int.Parse(s, CultureInfo.InvariantCulture);

// TODO: Point - изменяемый класс. Сделайте его записью (record Point(int X, int Y)),
// чтобы работали ToString, равенство по значению и выражение with.
class Point
{
    public int X;
    public int Y;
    public Point(int x, int y) { X = x; Y = y; }
}
