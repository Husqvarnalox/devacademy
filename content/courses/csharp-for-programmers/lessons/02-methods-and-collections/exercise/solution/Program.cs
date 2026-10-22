// Основная часть программы готова: она читает команды и печатает результаты методов.
// Напишите четыре метода внизу файла. Основную часть не меняйте.
Dictionary<string, int> counts = new Dictionary<string, int>();
string? raw = Console.ReadLine();
while (raw != null)
{
    string[] parts = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length > 0)
    {
        switch (parts[0])
        {
            case "words":
                counts = CountWords(string.Join(" ", parts, 1, parts.Length - 1));
                List<string> keys = new List<string>(counts.Keys);
                keys.Sort(StringComparer.Ordinal);
                foreach (string key in keys)
                {
                    Console.WriteLine($"{key}={counts[key]}");
                }
                break;
            case "count":
                Console.WriteLine(GetOrDefault(counts, parts[1], int.Parse(parts[2])));
                break;
            case "dedupe":
                List<int> original = new List<int>();
                for (int i = 1; i < parts.Length; i++)
                {
                    original.Add(int.Parse(parts[i]));
                }
                List<int> unique = Dedupe(original);
                Console.WriteLine(string.Join(" ", unique));
                Console.WriteLine($"исходный список: {original.Count}");
                break;
            case "rotate":
                int shift = int.Parse(parts[1]);
                int[] items = new int[parts.Length - 2];
                for (int i = 0; i < items.Length; i++)
                {
                    items[i] = int.Parse(parts[i + 2]);
                }
                Console.WriteLine(string.Join(" ", Rotate(items, shift)));
                Console.WriteLine(string.Join(" ", items));
                break;
        }
    }
    raw = Console.ReadLine();
}

static Dictionary<string, int> CountWords(string text)
{
    Dictionary<string, int> result = new Dictionary<string, int>();
    string[] words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    foreach (string word in words)
    {
        string key = word.ToLowerInvariant();
        if (result.TryGetValue(key, out int current))
        {
            result[key] = current + 1;
        }
        else
        {
            result[key] = 1;
        }
    }
    return result;
}

static int GetOrDefault(Dictionary<string, int> counts, string key, int fallback)
{
    if (counts.TryGetValue(key, out int value))
    {
        return value;
    }
    return fallback;
}

static List<int> Dedupe(List<int> items)
{
    List<int> result = new List<int>();
    HashSet<int> seen = new HashSet<int>();
    foreach (int item in items)
    {
        if (seen.Add(item))
        {
            result.Add(item);
        }
    }
    return result;
}

static int[] Rotate(int[] items, int k)
{
    int n = items.Length;
    int[] result = new int[n];
    if (n == 0)
    {
        return result;
    }
    int shift = ((k % n) + n) % n;
    for (int i = 0; i < n; i++)
    {
        result[(i + shift) % n] = items[i];
    }
    return result;
}
