#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

string[] lines = Console.In.ReadToEnd().Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
int n = int.Parse(lines[0], CultureInfo.InvariantCulture);

List<Order> orders = lines
    .Skip(1)
    .Take(n)
    .Select(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries))
    .Select(p => new Order(p[0], p[1], int.Parse(p[2], CultureInfo.InvariantCulture)))
    .ToList();

Console.WriteLine("totals:");
var totals = orders
    .GroupBy(o => o.Customer)
    .Select(g => (Name: g.Key, Total: g.Sum(o => o.Amount)))
    .OrderByDescending(t => t.Total)
    .ThenBy(t => t.Name, StringComparer.Ordinal);
foreach (var t in totals)
    Console.WriteLine($"{t.Name} {t.Total}");

Console.WriteLine("categories:");
ILookup<string, Order> byCategory = orders.ToLookup(o => o.Category);
foreach (var cat in byCategory.OrderBy(g => g.Key, StringComparer.Ordinal))
{
    string names = string.Join(',', cat.Select(o => o.Customer).Distinct().Order(StringComparer.Ordinal));
    Console.WriteLine($"{cat.Key}: {names}");
}

Order? biggest = orders.MaxBy(o => o.Amount);
Console.WriteLine(biggest is null ? "biggest: none" : $"biggest: {biggest.Customer} {biggest.Amount}");

record Order(string Customer, string Category, int Amount);
