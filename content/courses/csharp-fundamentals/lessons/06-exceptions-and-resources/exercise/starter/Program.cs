#nullable enable
using System;
using System.Globalization;

string[] lines = Console.In.ReadToEnd().Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
int n = int.Parse(lines[0], CultureInfo.InvariantCulture);
int started = 0;

// TODO: вынесите ресурс в using, чтобы close печатался даже при исключении,
// ловите только InvalidOperationException с сообщением "failed ..." (фильтр when),
// а done печатайте в finally. Остальные исключения должны дойти до внешнего catch.
for (int i = 1; i <= n; i++)
{
    string[] p = lines[i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
    string name = p[0];
    string kind = p[1];
    started++;
    var resource = new Resource(name);
    try
    {
        Run(name, kind);
        resource.Dispose();
    }
    catch (Exception e)
    {
        Console.WriteLine($"caught {e.Message}");
    }
    Console.WriteLine($"done {name}");
}
Console.WriteLine($"finished {started}");

static void Run(string name, string kind)
{
    if (kind == "ok")
    {
        Console.WriteLine($"work {name}");
    }
    else if (kind == "fail")
    {
        Console.WriteLine($"work {name}");
        throw new InvalidOperationException($"failed {name}");
    }
    else if (kind == "bad")
    {
        throw new InvalidOperationException("bad state");
    }
    else if (kind == "fatal")
    {
        throw new NotSupportedException("fatal");
    }
    else if (kind.StartsWith("parse:", StringComparison.Ordinal))
    {
        string text = kind["parse:".Length..];
        Console.WriteLine(int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? $"parsed {value}"
            : $"invalid {text}");
    }
}

sealed class Resource : IDisposable
{
    private readonly string _name;

    public Resource(string name)
    {
        _name = name;
        Console.WriteLine($"open {name}");
    }

    public void Dispose() => Console.WriteLine($"close {_name}");
}
