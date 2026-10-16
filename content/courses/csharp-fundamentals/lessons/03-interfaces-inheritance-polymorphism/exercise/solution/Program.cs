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

abstract class Employee(int id, string name) : IEquatable<Employee>
{
    public int Id { get; } = id;
    public string Name { get; } = name;
    public string Kind => GetType().Name;
    public virtual string Currency => "USD";
    public abstract long Pay();

    public bool Equals(Employee? other) => other is not null && other.Id == Id;
    public override bool Equals(object? obj) => Equals(obj as Employee);
    public override int GetHashCode() => Id.GetHashCode();
}

sealed class Engineer(int id, string name, long basePay) : Employee(id, name)
{
    public override long Pay() => basePay;
}

sealed class Manager(int id, string name, long basePay, int reports) : Employee(id, name)
{
    public override long Pay() => basePay + 100L * reports;
}

sealed class Intern(int id, string name) : Employee(id, name)
{
    public override string Currency => "EUR";
    public override long Pay() => 500;
}
