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
    long sum = (long)a + b;
    return (int)(sum / 2);
}

static int FloorMod(int a, int b)
{
    return ((a % b) + b) % b;
}

static decimal OrderTotal(decimal price, int quantity, decimal discountPercent)
{
    decimal raw = price * quantity * (100 - discountPercent) / 100;
    return Math.Round(raw, 2, MidpointRounding.AwayFromZero);
}

static string Grade(int score) => score switch
{
    < 0 or > 100 => "?",
    >= 90 => "A",
    >= 75 => "B",
    >= 60 => "C",
    _ => "D"
};
