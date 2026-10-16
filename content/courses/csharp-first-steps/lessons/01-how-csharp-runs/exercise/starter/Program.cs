using System;
using System.Globalization;
using System.Linq;

string text = Console.In.ReadToEnd();
string[] tokens = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
int n = int.Parse(tokens[0], CultureInfo.InvariantCulture);
// TODO: преобразуйте следующие n токенов в long
long[] nums = new long[0];

// TODO: сумма, минимум, максимум, среднее
Console.WriteLine("sum=" + 0);
Console.WriteLine("min=" + 0);
Console.WriteLine("max=" + 0);
Console.WriteLine("avg=" + 0.0.ToString("F2", CultureInfo.InvariantCulture));
