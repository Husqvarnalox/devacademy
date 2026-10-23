using System.Globalization;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

Dictionary<string, decimal> totals = new Dictionary<string, decimal>();
decimal overall = 0;
int malformed = 0;

string? raw = Console.ReadLine();
while (raw != null)
{
    string line = raw.Trim();
    if (line.Length > 0)
    {
        string[] parts = line.Split(' ');
        if (parts.Length == 2 && decimal.TryParse(parts[1], out decimal amount))
        {
            string category = parts[0];
            if (totals.ContainsKey(category))
            {
                totals[category] = totals[category] + amount;
            }
            else
            {
                totals[category] = amount;
            }
            overall = overall + amount;
        }
        else
        {
            malformed++;
        }
    }
    raw = Console.ReadLine();
}

List<string> names = new List<string>(totals.Keys);
names.Sort(StringComparer.Ordinal);

string best = "";
decimal bestTotal = 0;
foreach (string name in names)
{
    Console.WriteLine($"{name}: {totals[name]:F2}");
    if (best == "" || totals[name] > bestTotal)
    {
        best = name;
        bestTotal = totals[name];
    }
}

Console.WriteLine($"Итого: {overall:F2}");
Console.WriteLine($"Больше всего: {(best == "" ? "нет" : best)}");
Console.WriteLine($"Неверных строк: {malformed}");
