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
    // TODO: число из строки или null, если строка не число или не помещается в int
    return 0;
}

static int SumNonNull(int?[] values)
{
    // TODO: сумма значений, которые не равны null (для пустого массива - 0)
    return -1;
}

static Counter Incremented(Counter counter)
{
    // TODO: вернуть счётчик на 1 больше; переданный в main оригинал меняться не должен
    return counter;
}

static void Bump(Box box)
{
    // TODO: увеличить box.Value на 1 прямо в переданном объекте
}

struct Counter
{
    public int Value;
}

class Box
{
    public int Value;
}
