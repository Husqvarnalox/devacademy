int n = int.Parse(Console.ReadLine() ?? "");
List<string> words = new List<string>();
for (int i = 0; i < n; i++)
{
    words.Add(Console.ReadLine() ?? "");
}
string query = Console.ReadLine() ?? "";

Dictionary<string, int> counts = CountWords(words);
Console.WriteLine($"Разных слов: {counts.Count}");
if (counts.TryGetValue(query, out int found))
{
    Console.WriteLine($"{query}: {found}");
}
else
{
    Console.WriteLine($"{query}: нет");
}

static Dictionary<string, int> CountWords(List<string> words)
{
    Dictionary<string, int> result = new Dictionary<string, int>();
    foreach (string word in words)
    {
        if (result.ContainsKey(word))
        {
            result[word] = result[word] + 1;
        }
        else
        {
            result[word] = 1;
        }
    }
    return result;
}
