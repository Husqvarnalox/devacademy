#nullable enable
using System;
using System.Globalization;

string[] lines = Console.In.ReadToEnd().Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
int n = int.Parse(lines[0], CultureInfo.InvariantCulture);

var current = new Account();
Account? snapshot = null;

for (int i = 1; i <= n; i++)
{
    string[] parts = lines[i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
    switch (parts[0])
    {
        case "deposit":
            current.Balance += long.Parse(parts[1], CultureInfo.InvariantCulture);
            break;
        case "snapshot":
            snapshot = current;
            break;
        case "restore":
            if (snapshot is { } s) current = s;
            else Console.WriteLine("error: no snapshot");
            break;
        case "show":
            string snap = snapshot is { } t ? t.Balance.ToString(CultureInfo.InvariantCulture) : "none";
            Console.WriteLine($"current={current.Balance.ToString(CultureInfo.InvariantCulture)} snapshot={snap}");
            break;
    }
}

struct Account
{
    public long Balance;
}
