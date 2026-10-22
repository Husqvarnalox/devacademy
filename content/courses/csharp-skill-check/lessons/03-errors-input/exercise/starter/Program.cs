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
    // TODO: запись верна, если это ровно две части через один пробел, а вторая - целое от 0 до 100
    //       без знака, пробелов и дробной части. При успехе верните имя и оценку, при неудаче - "" и 0.
    name = "";
    score = 0;
    return false;
}

static double Average(List<int> scores)
{
    // TODO: среднее арифметическое оценок; для пустого списка - 0
    return -1;
}
