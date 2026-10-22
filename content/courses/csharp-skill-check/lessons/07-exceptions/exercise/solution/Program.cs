using System.Globalization;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

// Основная часть программы готова: каждая строка ввода - попытка снять сумму с кошелька.
// Допишите внизу файла класс исключения, метод Withdraw и функцию Handle. Остальное не меняйте.
Wallet wallet = new Wallet(100);
string? raw = Console.ReadLine();
while (raw != null)
{
    Console.WriteLine(Handle(wallet, raw));
    raw = Console.ReadLine();
}

Console.WriteLine($"остаток: {wallet.Balance}");
Console.WriteLine($"незакрытых: {Log.Open}");

static string Handle(Wallet wallet, string line)
{
    using Log log = new Log();
    try
    {
        int amount = int.Parse(line);
        wallet.Withdraw(amount);
        return $"ок: остаток {wallet.Balance}";
    }
    catch (InsufficientFundsException ex)
    {
        return $"отказ: не хватает {ex.Missing}";
    }
    catch (ArgumentOutOfRangeException)
    {
        return "отказ: сумма должна быть положительной";
    }
    catch (FormatException)
    {
        return "отказ: не число";
    }
    catch (OverflowException)
    {
        return "отказ: слишком большое число";
    }
}

class InsufficientFundsException : Exception
{
    public int Missing { get; }

    public InsufficientFundsException(int missing) : base($"не хватает {missing}")
    {
        Missing = missing;
    }
}

class Wallet
{
    public Wallet(int balance)
    {
        Balance = balance;
    }

    public int Balance { get; private set; }

    public void Withdraw(int amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }
        if (amount > Balance)
        {
            throw new InsufficientFundsException(amount - Balance);
        }
        Balance -= amount;
    }
}

class Log : IDisposable
{
    public static int Open { get; private set; }

    public Log()
    {
        Open++;
    }

    public void Dispose()
    {
        Open--;
    }
}
