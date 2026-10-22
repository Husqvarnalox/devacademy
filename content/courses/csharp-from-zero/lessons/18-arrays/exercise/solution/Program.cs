int n = int.Parse(Console.ReadLine() ?? "");
int[] temps = new int[n];
for (int i = 0; i < n; i++)
{
    temps[i] = int.Parse(Console.ReadLine() ?? "");
}
int limit = int.Parse(Console.ReadLine() ?? "");

Console.WriteLine($"Сумма: {Sum(temps)}");
Console.WriteLine($"Максимум: {Max(temps)}");
Console.WriteLine($"Выше {limit}: {CountAbove(temps, limit)}");

static int Sum(int[] values)
{
    int sum = 0;
    foreach (int v in values)
    {
        sum += v;
    }
    return sum;
}

static int Max(int[] values)
{
    int max = values[0];
    foreach (int v in values)
    {
        if (v > max)
        {
            max = v;
        }
    }
    return max;
}

static int CountAbove(int[] values, int limit)
{
    int count = 0;
    foreach (int v in values)
    {
        if (v > limit)
        {
            count++;
        }
    }
    return count;
}
