// Основная часть программы готова: она читает команды (по одной на строку) и печатает результат методов.
// Напишите четыре метода внизу файла. Основную часть не меняйте.
string? raw = Console.ReadLine();
while (raw != null)
{
    string[] parts = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length > 0)
    {
        string text = string.Join(" ", parts, 1, parts.Length - 1);
        switch (parts[0])
        {
            case "sum":
                int[] values = new int[parts.Length - 1];
                for (int i = 1; i < parts.Length; i++)
                {
                    values[i - 1] = int.Parse(parts[i]);
                }
                Console.WriteLine(string.Join(" ", RunningSum(values)));
                break;
            case "common":
                string word = MostCommonWord(text);
                Console.WriteLine(word == "" ? "(пусто)" : word);
                break;
            case "unique":
                List<string> items = new List<string>(parts);
                items.RemoveAt(0);
                Console.WriteLine(string.Join(" ", Unique(items)));
                break;
            case "pal":
                Console.WriteLine(IsPalindrome(text) ? "да" : "нет");
                break;
        }
    }
    raw = Console.ReadLine();
}

static int[] RunningSum(int[] values)
{
    int[] result = new int[values.Length];
    int total = 0;
    for (int i = 0; i < values.Length; i++)
    {
        total += values[i];
        result[i] = total;
    }
    return result;
}

static string MostCommonWord(string text)
{
    Dictionary<string, int> counts = new Dictionary<string, int>();
    foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
    {
        string key = word.ToLowerInvariant();
        counts[key] = counts.GetValueOrDefault(key) + 1;
    }
    string best = "";
    int bestCount = 0;
    foreach (KeyValuePair<string, int> pair in counts)
    {
        bool better = pair.Value > bestCount
            || (pair.Value == bestCount && string.CompareOrdinal(pair.Key, best) < 0);
        if (better)
        {
            best = pair.Key;
            bestCount = pair.Value;
        }
    }
    return best;
}

static List<string> Unique(List<string> items)
{
    HashSet<string> seen = new HashSet<string>(items);
    List<string> result = new List<string>(seen);
    result.Sort(StringComparer.Ordinal);
    return result;
}

static bool IsPalindrome(string text)
{
    List<char> letters = new List<char>();
    foreach (char c in text)
    {
        if (char.IsLetter(c))
        {
            letters.Add(char.ToLowerInvariant(c));
        }
    }
    for (int i = 0; i < letters.Count / 2; i++)
    {
        if (letters[i] != letters[letters.Count - 1 - i])
        {
            return false;
        }
    }
    return true;
}
