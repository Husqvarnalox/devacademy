// Основная часть программы готова: она читает команды (по одной на строку) и печатает результат.
// Допишите четыре метода внизу файла. Основную часть, метод Numbers и запись Entry не меняйте.
List<Entry> entries = new List<Entry>();
string? raw = Console.ReadLine();
while (raw != null)
{
    string[] parts = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length > 0)
    {
        switch (parts[0])
        {
            case "add":
                entries.Add(new Entry(parts[1], int.Parse(parts[2])));
                break;
            case "report":
                List<string> lines = TotalsByCategory(entries);
                Console.WriteLine(lines.Count == 0 ? "пусто" : string.Join("; ", lines));
                break;
            case "squares":
                List<int> squares = EvenSquares(Numbers(parts, 1));
                Console.WriteLine(squares.Count == 0 ? "нет" : string.Join(" ", squares));
                break;
            case "top":
                List<int> top = TopN(Numbers(parts, 2), int.Parse(parts[1]));
                Console.WriteLine(top.Count == 0 ? "нет" : string.Join(" ", top));
                break;
            case "over":
                int? found = FirstOver(Numbers(parts, 2), int.Parse(parts[1]));
                Console.WriteLine(found.HasValue ? found.Value.ToString() : "нет");
                break;
        }
    }
    raw = Console.ReadLine();
}

static List<int> Numbers(string[] parts, int start)
{
    List<int> result = new List<int>();
    for (int i = start; i < parts.Length; i++)
    {
        result.Add(int.Parse(parts[i]));
    }
    return result;
}

static List<string> TotalsByCategory(List<Entry> entries)
{
    return entries
        .GroupBy(e => e.Category)
        .Select(g => new { Name = g.Key, Total = g.Sum(e => e.Amount) })
        .OrderByDescending(x => x.Total)
        .ThenBy(x => x.Name, StringComparer.Ordinal)
        .Select(x => $"{x.Name}={x.Total}")
        .ToList();
}

static List<int> EvenSquares(List<int> values)
{
    return values
        .Where(v => v % 2 == 0)
        .Select(v => v * v)
        .OrderBy(v => v)
        .ToList();
}

static List<int> TopN(List<int> values, int n)
{
    return values.OrderByDescending(v => v).Take(n).ToList();
}

static int? FirstOver(List<int> values, int limit)
{
    List<int> found = values.Where(v => v > limit).Take(1).ToList();
    if (found.Count == 0)
    {
        return null;
    }
    return found[0];
}

record Entry(string Category, int Amount);
