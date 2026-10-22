using System.Globalization;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

// Основная часть программы готова: она читает команды и обращается к вашему коду.
// Допишите методы TryParseAmount и Describe и класс Account внизу файла. Основную часть не меняйте.
Dictionary<string, Account> accounts = new Dictionary<string, Account>();
string? raw = Console.ReadLine();
while (raw != null)
{
    string[] parts = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length == 2 && parts[0] == "show")
    {
        Console.WriteLine(Describe(accounts.GetValueOrDefault(parts[1])));
    }
    else if (parts.Length == 3)
    {
        string command = parts[0];
        string owner = parts[1];
        if (!TryParseAmount(parts[2], out decimal amount))
        {
            Console.WriteLine("неверная сумма");
        }
        else if (command == "open")
        {
            if (accounts.ContainsKey(owner))
            {
                Console.WriteLine("счёт уже есть");
            }
            else
            {
                try
                {
                    accounts[owner] = new Account(owner, amount);
                    Console.WriteLine($"открыт: {Describe(accounts[owner])}");
                }
                catch (ArgumentException)
                {
                    Console.WriteLine("неверная сумма");
                }
            }
        }
        else if (command == "deposit" || command == "withdraw")
        {
            Account? account = accounts.GetValueOrDefault(owner);
            if (account == null)
            {
                Console.WriteLine($"нет счёта: {owner}");
            }
            else
            {
                try
                {
                    if (command == "deposit")
                    {
                        account.Deposit(amount);
                    }
                    else
                    {
                        account.Withdraw(amount);
                    }
                    Console.WriteLine(Describe(account));
                }
                catch (ArgumentException)
                {
                    Console.WriteLine("неверная сумма");
                }
                catch (InvalidOperationException)
                {
                    Console.WriteLine("недостаточно средств");
                }
            }
        }
        else
        {
            Console.WriteLine("неизвестная команда");
        }
    }
    else
    {
        Console.WriteLine("неизвестная команда");
    }
    raw = Console.ReadLine();
}

static bool TryParseAmount(string text, out decimal amount)
{
    NumberStyles styles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
    return decimal.TryParse(text, styles, CultureInfo.InvariantCulture, out amount);
}

static string Describe(Account? account)
{
    if (account == null)
    {
        return "нет счёта";
    }
    return $"{account.Owner}: {account.Balance:F2}";
}

class Account
{
    public string Owner { get; }
    public decimal Balance { get; private set; }

    public Account(string owner, decimal opening)
    {
        if (opening < 0)
        {
            throw new ArgumentException("начальный баланс не может быть отрицательным", nameof(opening));
        }
        Owner = owner;
        Balance = opening;
    }

    public void Deposit(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("сумма должна быть положительной", nameof(amount));
        }
        Balance += amount;
    }

    public void Withdraw(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("сумма должна быть положительной", nameof(amount));
        }
        if (amount > Balance)
        {
            throw new InvalidOperationException("недостаточно средств");
        }
        Balance -= amount;
    }
}
