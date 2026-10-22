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
    // TODO: "FizzBuzz" для кратных 3 и 5, "Fizz" для кратных 3, "Buzz" для кратных 5, иначе число строкой
    return "";
}

static int DigitSum(int n)
{
    // TODO: сумма десятичных цифр неотрицательного числа n (для 0 - 0)
    return -1;
}

static bool IsLeapYear(int year)
{
    // TODO: високосный год: кратен 4, но не 100, кроме кратных 400
    return false;
}

static int NextPrime(int n)
{
    // TODO: наименьшее простое число, строго большее n (n может быть отрицательным)
    return 0;
}
