using System.Globalization;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

// Основная часть программы готова: она читает команды (по одной на строку) и печатает результат методов.
// Напишите четыре метода внизу файла. Основную часть не меняйте.
string? raw = Console.ReadLine();
while (raw != null)
{
    string[] parts = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length == 2)
    {
        int value = int.Parse(parts[1]);
        switch (parts[0])
        {
            case "fizz":
                Console.WriteLine(FizzBuzz(value));
                break;
            case "digits":
                Console.WriteLine(DigitSum(value));
                break;
            case "leap":
                Console.WriteLine(IsLeapYear(value) ? "да" : "нет");
                break;
            case "prime":
                Console.WriteLine(NextPrime(value));
                break;
        }
    }
    raw = Console.ReadLine();
}

static string FizzBuzz(int n)
{
    if (n % 15 == 0)
    {
        return "FizzBuzz";
    }
    if (n % 3 == 0)
    {
        return "Fizz";
    }
    if (n % 5 == 0)
    {
        return "Buzz";
    }
    return n.ToString();
}

static int DigitSum(int n)
{
    int total = 0;
    while (n > 0)
    {
        total += n % 10;
        n /= 10;
    }
    return total;
}

static bool IsLeapYear(int year)
{
    return (year % 4 == 0 && year % 100 != 0) || year % 400 == 0;
}

static bool IsPrime(int n)
{
    if (n < 2)
    {
        return false;
    }
    for (int d = 2; d * d <= n; d++)
    {
        if (n % d == 0)
        {
            return false;
        }
    }
    return true;
}

static int NextPrime(int n)
{
    int candidate = n + 1;
    while (!IsPrime(candidate))
    {
        candidate++;
    }
    return candidate;
}
