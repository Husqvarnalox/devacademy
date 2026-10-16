#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

string[] lines = Console.In.ReadToEnd().Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
int n = int.Parse(lines[0], CultureInfo.InvariantCulture);

var totals = new SortedDictionary<string, Money>(StringComparer.Ordinal);
var seen = new HashSet<Money>();

for (int i = 1; i <= n; i++)
{
    string[] parts = lines[i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length != 2 || !Money.TryParse(parts[0], parts[1], out Money money))
    {
        Console.WriteLine($"invalid: {lines[i]}");
        continue;
    }
    seen.Add(money);
    totals[money.Currency] = totals.TryGetValue(money.Currency, out Money sum) ? sum + money : money;
}

foreach (var (_, total) in totals) Console.WriteLine(total);
Console.WriteLine($"distinct={seen.Count}");

readonly record struct Money(long Cents, string Currency)
{
    public static Money operator +(Money a, Money b) =>
        a.Currency == b.Currency
            ? new Money(a.Cents + b.Cents, a.Currency)
            : throw new InvalidOperationException("currency mismatch");

    public static bool TryParse(string amount, string currency, out Money money)
    {
        money = default;
        if (!decimal.TryParse(amount, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal d))
            return false;
        decimal cents = d * 100m;
        if (cents != decimal.Truncate(cents)) return false;
        money = new Money((long)cents, currency);
        return true;
    }

    public override string ToString()
    {
        string sign = Cents < 0 ? "-" : "";
        long abs = Math.Abs(Cents);
        return $"{Currency} {sign}{abs / 100}.{abs % 100:00}";
    }
}
