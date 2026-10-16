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

// TODO 3: возьмите последние (не более) три допустимых числа БЕЗ копирования:
// ReadOnlySpan<int> над buffer[0..count] и срез через диапазон. Сложите их в tailSum.
long tailSum = 0;

Console.WriteLine($"valid={count} invalid={invalid}");
Console.WriteLine($"sum={sum.ToString(CultureInfo.InvariantCulture)}");
Console.WriteLine($"min={Format(min)} max={Format(max)}");
Console.WriteLine($"tail={tailSum.ToString(CultureInfo.InvariantCulture)}");

// TODO 1: метод вызывается как TryParseToken(token, out int value), но параметр объявлен без out.
// Исправьте сигнатуру (out int result) и разберите токен через int.TryParse с InvariantCulture.
static bool TryParseToken(string token, int result)
{
    return false;
}

// TODO 2: min и max должны меняться у вызывающего - сделайте параметры ref.
static void Track(int? min, int? max, int value)
{
    if (min is null || value < min) min = value;
    if (max is null || value > max) max = value;
}

static string Format(int? value) => value is { } v ? v.ToString(CultureInfo.InvariantCulture) : "none";
