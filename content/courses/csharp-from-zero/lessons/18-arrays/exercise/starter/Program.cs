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
    // TODO: верните сумму всех элементов values
    return 0;
}

static int Max(int[] values)
{
    // TODO: верните наибольший элемент values (начните с values[0])
    return 0;
}

static int CountAbove(int[] values, int limit)
{
    // TODO: верните, сколько элементов values строго больше limit
    return 0;
}
