#nullable enable
using System;
using System.Globalization;

string[] lines = Console.In.ReadToEnd().Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
int n = int.Parse(lines[0], CultureInfo.InvariantCulture);
int started = 0;

try
{
    for (int i = 1; i <= n; i++)
    {
        string[] p = lines[i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string name = p[0];
        string kind = p[1];
        started++;
        try
        {
            using var resource = new Resource(name);
            Run(name, kind);
        }
        catch (InvalidOperationException e) when (e.Message.StartsWith("failed ", StringComparison.Ordinal))
        {
            Console.WriteLine($"caught {e.Message}");
        }
        finally
        {
            Console.WriteLine($"done {name}");
        }
    }
}
catch (Exception e)
{
    Console.WriteLine($"unhandled {e.GetType().Name}");
}
finally
{
    Console.WriteLine($"finished {started}");
}

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
