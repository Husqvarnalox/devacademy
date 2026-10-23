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
    // TODO: строки вида «категория=сумма», по убыванию суммы, при равенстве - по названию (StringComparer.Ordinal)
    return new List<string>();
}

static List<int> EvenSquares(List<int> values)
{
    // TODO: квадраты чётных чисел по возрастанию
    return new List<int>();
}

static List<int> TopN(List<int> values, int n)
{
    // TODO: n наибольших значений по убыванию (повторы сохраняются; если значений меньше n - все)
    return new List<int>();
}

static int? FirstOver(List<int> values, int limit)
{
    // TODO: первое значение, строго большее limit, в порядке списка; если такого нет - null
    return 0;
}

record Entry(string Category, int Amount);
