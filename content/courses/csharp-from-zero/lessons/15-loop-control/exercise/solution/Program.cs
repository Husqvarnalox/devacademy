int total = 0;
int skipped = 0;

while (true)
{
    string? line = Console.ReadLine();
    if (line == null || line == "0")
    {
        break;
    }

    int price = int.Parse(line);
    if (price < 0)
    {
        skipped++;
        continue;
    }

    total += price;
}

Console.WriteLine($"Сумма: {total}");
Console.WriteLine($"Пропущено: {skipped}");
