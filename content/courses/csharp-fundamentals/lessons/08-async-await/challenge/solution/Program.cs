#nullable enable
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

string[] lines = Console.In.ReadToEnd().Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
int limit = int.Parse(lines[0], CultureInfo.InvariantCulture);
int n = int.Parse(lines[1], CultureInfo.InvariantCulture);

var jobs = lines.Skip(2).Take(n)
    .Select(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries))
    .Select(p => (Name: p[0], DelayMs: int.Parse(p[1], CultureInfo.InvariantCulture)))
    .ToList();

var gate = new SemaphoreSlim(limit);
var counter = new PeakCounter();

Task<string>[] tasks = jobs.Select(j => RunAsync(j.Name, j.DelayMs)).ToArray();
string[] results = await Task.WhenAll(tasks);

foreach (string r in results)
    Console.WriteLine(r);
Console.WriteLine($"peak={counter.Peak}");

async Task<string> RunAsync(string name, int delayMs)
{
    await gate.WaitAsync();
    try
    {
        counter.Enter();
        try
        {
            await Task.Delay(delayMs);
        }
        finally
        {
            counter.Leave();
        }
        return $"{name} -> {name.Length}";
    }
    finally
    {
        gate.Release();
    }
}

sealed class PeakCounter
{
    private int _running;
    private int _peak;

    public int Peak => Volatile.Read(ref _peak);

    public void Enter()
    {
        int now = Interlocked.Increment(ref _running);
        int seen;
        while (now > (seen = Volatile.Read(ref _peak)))
        {
            if (Interlocked.CompareExchange(ref _peak, now, seen) == seen) break;
        }
    }

    public void Leave() => Interlocked.Decrement(ref _running);
}
