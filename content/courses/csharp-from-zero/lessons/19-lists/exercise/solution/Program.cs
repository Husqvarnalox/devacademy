List<int> numbers = new List<int>();
string? line = Console.ReadLine();
while (line != null)
{
    numbers.Add(int.Parse(line));
    line = Console.ReadLine();
}

List<int> positive = Positive(numbers);
positive.Sort();
Console.WriteLine($"Положительных: {positive.Count}");
Console.WriteLine($"Сумма: {Total(positive)}");
Console.WriteLine($"Список: [{string.Join(" ", positive)}]");

static List<int> Positive(List<int> numbers)
{
    List<int> result = new List<int>();
    foreach (int n in numbers)
    {
        if (n > 0)
        {
            result.Add(n);
        }
    }
    return result;
}

static int Total(List<int> numbers)
{
    int sum = 0;
    foreach (int n in numbers)
    {
        sum += n;
    }
    return sum;
}
