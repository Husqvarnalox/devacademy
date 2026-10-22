using System.Globalization;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

Dictionary<string, decimal> totals = new Dictionary<string, decimal>();
decimal overall = 0;
int malformed = 0;

string? raw = Console.ReadLine();
while (raw != null)
{
    // TODO: уберите пробелы по краям (Trim); пустую строку пропустите;
    //       разберите строку на категорию и сумму; неверную строку посчитайте в malformed;
    //       верную добавьте в totals и в overall
    raw = Console.ReadLine();
}

// TODO: выведите по строке "название: сумма" для каждой категории (по алфавиту, два знака после запятой)
Console.WriteLine($"Итого: {overall:F2}");
// TODO: выведите "Больше всего: название" (или "нет", если категорий нет)
Console.WriteLine($"Неверных строк: {malformed}");
