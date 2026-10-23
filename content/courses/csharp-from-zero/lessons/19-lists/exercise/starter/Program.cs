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
    // TODO: верните новый список из чисел numbers, которые строго больше 0
    return new List<int>();
}

static int Total(List<int> numbers)
{
    // TODO: верните сумму чисел списка (для пустого списка - 0)
    return 0;
}
