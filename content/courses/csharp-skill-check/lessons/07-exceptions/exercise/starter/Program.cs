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
    // TODO: открыть журнал (Log) так, чтобы он закрывался при любом исходе, разобрать line как int
    //       и снять сумму. Результат - одна из строк:
    //       "ок: остаток N", "отказ: не число", "отказ: слишком большое число",
    //       "отказ: сумма должна быть положительной", "отказ: не хватает M".
    return "?";
}

class InsufficientFundsException : Exception
{
    // TODO: свойство Missing (int) - сколько не хватает; конструктор принимает это число
    //       и передаёт в базовый класс сообщение.
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
        // TODO: при amount <= 0 бросить ArgumentOutOfRangeException, при нехватке средств -
        //       InsufficientFundsException, иначе уменьшить баланс. Баланс при ошибке не меняется.
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
