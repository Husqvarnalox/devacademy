// Основная часть программы готова: она читает команды (по одной на строку) и печатает результат.
// Допишите четыре метода внизу файла. Основную часть и описания типов не меняйте.
string? raw = Console.ReadLine();
while (raw != null)
{
    string[] parts = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length > 0)
    {
        switch (parts[0])
        {
            case "parse":
                int? parsed = ParseOrNull(parts[1]);
                Console.WriteLine(parsed.HasValue ? parsed.Value.ToString() : "null");
                break;
            case "sum":
                int?[] values = new int?[parts.Length - 1];
                for (int i = 1; i < parts.Length; i++)
                {
                    values[i - 1] = parts[i] == "_" ? null : int.Parse(parts[i]);
                }
                Console.WriteLine(SumNonNull(values));
                break;
            case "counter":
                Counter original = new Counter { Value = int.Parse(parts[1]) };
                Counter next = Incremented(original);
                Console.WriteLine($"{original.Value} {next.Value}");
                break;
            case "box":
                Box box = new Box { Value = int.Parse(parts[1]) };
                Bump(box);
                Console.WriteLine(box.Value);
                break;
        }
    }
    raw = Console.ReadLine();
}

static int? ParseOrNull(string text)
{
    if (int.TryParse(text, out int value))
    {
        return value;
    }
    return null;
}

static int SumNonNull(int?[] values)
{
    int total = 0;
    foreach (int? value in values)
    {
        if (value.HasValue)
        {
            total += value.Value;
        }
    }
    return total;
}

static Counter Incremented(Counter counter)
{
    counter.Value++;
    return counter;
}

static void Bump(Box box)
{
    box.Value++;
}

struct Counter
{
    public int Value;
}

class Box
{
    public int Value;
}
