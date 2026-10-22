int n = int.Parse(Console.ReadLine() ?? "");

int first = int.Parse(Console.ReadLine() ?? "");
int max = first;
int min = first;

for (int i = 1; i < n; i++)
{
    int t = int.Parse(Console.ReadLine() ?? "");
    if (t > max)
    {
        max = t;
    }
    if (t < min)
    {
        min = t;
    }
}

Console.WriteLine($"Максимум: {max}");
Console.WriteLine($"Минимум: {min}");
