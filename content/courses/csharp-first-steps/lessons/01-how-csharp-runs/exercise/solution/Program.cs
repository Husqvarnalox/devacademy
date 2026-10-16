using System;
using System.Globalization;
using System.Linq;

string text = Console.In.ReadToEnd();
string[] tokens = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
int n = int.Parse(tokens[0], CultureInfo.InvariantCulture);
long[] nums = tokens.Skip(1).Take(n).Select(t => long.Parse(t, CultureInfo.InvariantCulture)).ToArray();

long sum = nums.Sum();
double avg = (double)sum / nums.Length;
Console.WriteLine($"sum={sum}");
Console.WriteLine($"min={nums.Min()}");
Console.WriteLine($"max={nums.Max()}");
Console.WriteLine("avg=" + avg.ToString("F2", CultureInfo.InvariantCulture));
