#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

string[] lines = Console.In.ReadToEnd().Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
int n = int.Parse(lines[0], CultureInfo.InvariantCulture);

List<Order> orders = new();
for (int i = 1; i <= n; i++)
{
    string[] p = lines[i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
    orders.Add(new Order(p[0], p[1], int.Parse(p[2], CultureInfo.InvariantCulture)));
}

// TODO: сгруппируйте заказы по клиенту (GroupBy), просуммируйте суммы
// и отсортируйте по сумме по убыванию, затем по имени.
Console.WriteLine("totals:");
foreach (var o in orders.OrderBy(o => o.Customer, StringComparer.Ordinal))
    Console.WriteLine($"{o.Customer} {o.Amount}");

// TODO: ToLookup по категории, клиенты без повторов, отсортированные.
Console.WriteLine("categories:");
foreach (var o in orders)
    Console.WriteLine($"{o.Category}: {o.Customer}");

// TODO: самый крупный заказ (MaxBy). При равенстве - первый по порядку ввода.
Console.WriteLine("biggest: none");

record Order(string Customer, string Category, int Amount);
