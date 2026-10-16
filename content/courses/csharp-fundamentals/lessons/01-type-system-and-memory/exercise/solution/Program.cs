#nullable enable
using System;
using System.Globalization;

string[] tokens = Console.In.ReadToEnd().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

int[] buffer = new int[tokens.Length];
int count = 0;
int invalid = 0;
long sum = 0;
int? min = null;
int? max = null;

foreach (string token in tokens)
{
    if (TryParseToken(token, out int value))
    {
        buffer[count++] = value;
        sum += value;
        Track(ref min, ref max, value);
    }
    else
    {
        invalid++;
    }
}

// Срез без копирования: последние (не более) три допустимых числа.
ReadOnlySpan<int> valid = buffer.AsSpan(0, count);
ReadOnlySpan<int> tail = valid[Math.Max(0, count - 3)..];
long tailSum = 0;
foreach (int v in tail) tailSum += v;

Console.WriteLine($"valid={count} invalid={invalid}");
Console.WriteLine($"sum={sum.ToString(CultureInfo.InvariantCulture)}");
Console.WriteLine($"min={Format(min)} max={Format(max)}");
Console.WriteLine($"tail={tailSum.ToString(CultureInfo.InvariantCulture)}");

static bool TryParseToken(string token, out int result) =>
    int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

static void Track(ref int? min, ref int? max, int value)
{
    if (min is null || value < min) min = value;
    if (max is null || value > max) max = value;
}

static string Format(int? value) => value is { } v ? v.ToString(CultureInfo.InvariantCulture) : "none";
