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
    // TODO: Money.TryParse должен отвергать неверные суммы (нечисла, больше двух знаков после запятой)
    // и неверное число частей строки. Для них печатается: invalid: <исходная строка>
    Money.TryParse(parts[0], parts[1], out Money money);
    seen.Add(money);
    totals[money.Currency] = totals.TryGetValue(money.Currency, out Money sum) ? sum + money : money;
}

foreach (var (_, total) in totals) Console.WriteLine(total);
Console.WriteLine($"distinct={seen.Count}");

// TODO: Money - изменяемый класс без равенства по значению. Сделайте его readonly record struct
// (Cents - целое число центов), добавьте оператор + для одной валюты и ToString вида "USD 12.50".
class Money
{
    public long Cents;
    public string Currency = "";

    public static Money operator +(Money a, Money b) => new Money { Cents = a.Cents + b.Cents, Currency = a.Currency };

    public static bool TryParse(string amount, string currency, out Money money)
    {
        money = new Money { Cents = (long)(double.Parse(amount, CultureInfo.InvariantCulture) * 100), Currency = currency };
        return true;
    }

    public override string ToString() => $"{Currency} {(Cents / 100.0).ToString("F2", CultureInfo.InvariantCulture)}";
}
