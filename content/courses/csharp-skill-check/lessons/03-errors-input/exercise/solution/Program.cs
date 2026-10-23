using System.Globalization;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

// Основная часть программы готова: она читает записи вида «имя оценка» до конца ввода и печатает итоги.
// Допишите два метода внизу файла. Основную часть не меняйте.
List<int> scores = new List<int>();
int invalid = 0;
string best = "";
int bestScore = -1;
string? raw = Console.ReadLine();
while (raw != null)
{
    string line = raw.Trim();
    if (line.Length > 0)
    {
        if (TryParseEntry(line, out string name, out int score))
        {
            scores.Add(score);
            if (score > bestScore)
            {
                best = name;
                bestScore = score;
            }
        }
        else
        {
            invalid++;
        }
    }
    raw = Console.ReadLine();
}

Console.WriteLine($"верных: {scores.Count}");
Console.WriteLine($"неверных: {invalid}");
Console.WriteLine($"среднее: {Average(scores):F2}");
Console.WriteLine($"лучший: {(best == "" ? "нет" : best)}");

static bool TryParseEntry(string line, out string name, out int score)
{
    name = "";
    score = 0;
    string[] parts = line.Split(' ');
    if (parts.Length != 2 || parts[0].Length == 0)
    {
        return false;
    }
    if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int value))
    {
        return false;
    }
    if (value < 0 || value > 100)
    {
        return false;
    }
    name = parts[0];
    score = value;
    return true;
}

static double Average(List<int> scores)
{
    if (scores.Count == 0)
    {
        return 0;
    }
    int total = 0;
    foreach (int s in scores)
    {
        total += s;
    }
    return (double)total / scores.Count;
}
