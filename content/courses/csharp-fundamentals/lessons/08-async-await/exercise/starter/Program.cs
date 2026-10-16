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

// TODO: создайте CancellationTokenSource с таймаутом и передайте токен в FetchAsync.
// Все запросы должны идти параллельно (Task.WhenAll), а результат - в порядке ввода.
var tasks = new List<Task<string>>();
foreach (var r in requests)
    tasks.Add(FetchAsync(r.Name, r.DelayMs, CancellationToken.None));
await Task.WhenAll(tasks);

int ok = 0;
int cancelled = 0;
for (int i = 0; i < tasks.Count; i++)
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
    // TODO: токен нужно передать в Task.Delay
    await Task.Delay(delayMs);
    return name;
}
