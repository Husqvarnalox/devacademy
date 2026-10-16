#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

string[] lines = Console.In.ReadToEnd().Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
int n = int.Parse(lines[0], CultureInfo.InvariantCulture);

var seen = new HashSet<Employee>();
var accepted = new List<Employee>();
int duplicates = 0;

for (int i = 1; i <= n; i++)
{
    string[] a = lines[i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
    Employee? e = a[0] switch
    {
        "engineer" => new Engineer(ParseInt(a[1]), a[2], ParseLong(a[3])),
        "manager" => new Manager(ParseInt(a[1]), a[2], ParseLong(a[3]), ParseInt(a[4])),
        "intern" => new Intern(ParseInt(a[1]), a[2]),
        _ => null,
    };
    if (e is null) { Console.WriteLine($"unknown: {a[0]}"); continue; }
    if (!seen.Add(e)) { duplicates++; continue; }
    accepted.Add(e);
    Console.WriteLine($"{e.Kind} {e.Name} pay={e.Pay()} {e.Currency}");
}

long usd = 0, eur = 0;
foreach (Employee e in accepted)
{
    if (e.Currency == "EUR") eur += e.Pay(); else usd += e.Pay();
}
Console.WriteLine($"total USD={usd} EUR={eur}");
Console.WriteLine($"duplicates={duplicates}");

static int ParseInt(string s) => int.Parse(s, CultureInfo.InvariantCulture);
static long ParseLong(string s) => long.Parse(s, CultureInfo.InvariantCulture);

// TODO: сделайте иерархию полиморфной.
// 1. Pay и Currency должны быть virtual (или Pay - abstract), а в наследниках - override вместо new.
// 2. Employee должен сравниваться по Id: реализуйте IEquatable<Employee>, Equals(object?) и GetHashCode,
//    чтобы HashSet отбрасывал повторные идентификаторы.
class Employee(int id, string name)
{
    public int Id { get; } = id;
    public string Name { get; } = name;
    public string Kind => GetType().Name;
    public string Currency => "USD";
    public long Pay() => 0;
}

class Engineer(int id, string name, long basePay) : Employee(id, name)
{
    public new long Pay() => basePay;
}

class Manager(int id, string name, long basePay, int reports) : Employee(id, name)
{
    public new long Pay() => basePay + 100L * reports;
}

class Intern(int id, string name) : Employee(id, name)
{
    public new string Currency => "EUR";
    public new long Pay() => 500;
}
