#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

string[] lines = Console.In.ReadToEnd().Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
int timeoutMs = int.Parse(lines[0], CultureInfo.InvariantCulture);
int n = int.Parse(lines[1], CultureInfo.InvariantCulture);

var requests = lines.Skip(2).Take(n)
    .Select(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries))
    .Select(p => (Name: p[0], DelayMs: int.Parse(p[1], CultureInfo.InvariantCulture)))
    .ToList();

using var cts = new CancellationTokenSource(timeoutMs);
Task<string>[] tasks = requests
    .Select(r => FetchAsync(r.Name, r.DelayMs, cts.Token))
    .ToArray();

try
{
    await Task.WhenAll(tasks);
}
catch (OperationCanceledException)
{
    // Статус каждой задачи проверяется ниже.
}

int ok = 0;
int cancelled = 0;
for (int i = 0; i < tasks.Length; i++)
{
    if (tasks[i].IsCanceled)
    {
        cancelled++;
        Console.WriteLine($"{requests[i].Name}: cancelled");
    }
    else
    {
        ok++;
        Console.WriteLine($"{requests[i].Name}: ok");
    }
}
Console.WriteLine($"ok={ok} cancelled={cancelled}");

static async Task<string> FetchAsync(string name, int delayMs, CancellationToken token)
{
    await Task.Delay(delayMs, token);
    return name;
}
