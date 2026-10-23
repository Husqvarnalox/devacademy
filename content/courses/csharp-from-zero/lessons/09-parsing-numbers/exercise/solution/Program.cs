using System.Globalization;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

decimal price = decimal.Parse(Console.ReadLine() ?? "");
int count = int.Parse(Console.ReadLine() ?? "");
decimal total = price * count;

Console.WriteLine($"Сумма: {total:F2}");
Console.WriteLine($"Сдача с 1000: {1000 - total:F2}");
