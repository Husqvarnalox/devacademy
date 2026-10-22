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
    // TODO: разобрать сумму строго: допустимы цифры, одна точка и знак в начале;
    //       пробелы, запятая-разделитель тысяч, экспонента и буквы недопустимы. Культура - InvariantCulture.
    amount = 0;
    return false;
}

static string Describe(Account? account)
{
    // TODO: "нет счёта", если account равен null, иначе "Владелец: баланс" с двумя знаками (Anna: 120.50)
    return "";
}

class Account
{
    public string Owner { get; }
    public decimal Balance { get; private set; }

    public Account(string owner, decimal opening)
    {
        // TODO: бросьте ArgumentException, если opening меньше нуля; иначе запомните владельца и начальный баланс
        Owner = owner;
    }

    public void Deposit(decimal amount)
    {
        // TODO: ArgumentException, если amount не больше нуля; иначе увеличьте баланс
    }

    public void Withdraw(decimal amount)
    {
        // TODO: ArgumentException, если amount не больше нуля; InvalidOperationException, если amount больше баланса;
        //       иначе уменьшите баланс
    }
}
