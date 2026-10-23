int count = 0;
int bad = 0;
int total = 0;

string? line = Console.ReadLine();
while (line != null)
{
    int quantity = ParseQuantity(line);
    if (quantity < 0)
    {
        bad++;
    }
    else
    {
        count++;
        total += quantity;
    }
    line = Console.ReadLine();
}

Console.WriteLine($"Принято: {count}");
Console.WriteLine($"Пропущено: {bad}");
Console.WriteLine($"Среднее: {SafeDivide(total, count)}");

static int ParseQuantity(string text)
{
    if (int.TryParse(text, out int value) && value >= 0)
    {
        return value;
    }
    return -1;
}

static int SafeDivide(int total, int count)
{
    try
    {
        return total / count;
    }
    catch (DivideByZeroException)
    {
        return 0;
    }
}
