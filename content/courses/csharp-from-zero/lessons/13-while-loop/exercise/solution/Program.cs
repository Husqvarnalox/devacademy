int count = 0;
int total = 0;
int expensive = 0;

string? line = Console.ReadLine();
while (line != null)
{
    int price = int.Parse(line);
    count++;
    total += price;
    if (price > 500)
    {
        expensive++;
    }
    line = Console.ReadLine();
}

Console.WriteLine($"Позиций: {count}");
Console.WriteLine($"Итого: {total}");
Console.WriteLine($"Дорогих (больше 500): {expensive}");
