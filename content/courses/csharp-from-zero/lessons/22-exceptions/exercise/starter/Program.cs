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
    // TODO: верните целое число из text, если оно не меньше нуля; иначе верните -1 (используйте int.TryParse)
    return -1;
}

static int SafeDivide(int total, int count)
{
    // TODO: верните total / count, а при count == 0 верните 0 (используйте try/catch)
    return total / count;
}
