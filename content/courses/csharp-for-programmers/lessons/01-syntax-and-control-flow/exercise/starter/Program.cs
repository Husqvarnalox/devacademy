using System.Globalization;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

// Основная часть программы готова: она читает команды и печатает результаты методов.
// Напишите четыре метода внизу файла. Основную часть не меняйте.
string? raw = Console.ReadLine();
while (raw != null)
{
    string[] parts = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length > 0)
    {
        switch (parts[0])
        {
            case "avg":
                Console.WriteLine(Average(int.Parse(parts[1]), int.Parse(parts[2])));
                break;
            case "mod":
                Console.WriteLine(FloorMod(int.Parse(parts[1]), int.Parse(parts[2])));
                break;
            case "total":
                decimal total = OrderTotal(decimal.Parse(parts[1]), int.Parse(parts[2]), decimal.Parse(parts[3]));
                Console.WriteLine(total.ToString("F2"));
                break;
            case "grade":
                Console.WriteLine(Grade(int.Parse(parts[1])));
                break;
        }
    }
    raw = Console.ReadLine();
}

static int Average(int a, int b)
{
    // TODO: среднее двух чисел без переполнения, дробная часть отбрасывается к нулю
    return 0;
}

static int FloorMod(int a, int b)
{
    // TODO: остаток от деления a на b > 0 в диапазоне от 0 до b - 1, в том числе для отрицательных a
    return -1;
}

static decimal OrderTotal(decimal price, int quantity, decimal discountPercent)
{
    // TODO: price * quantity со скидкой discountPercent процентов, округление до копеек (половина - от нуля)
    return 0m;
}

static string Grade(int score)
{
    // TODO: "A" от 90, "B" от 75, "C" от 60, иначе "D"; оценка вне диапазона 0..100 - "?"
    return "";
}
