string name = Console.ReadLine() ?? "";
string text = Console.ReadLine() ?? "";

Console.WriteLine($"Имя: [{Capitalize(name.Trim())}]");
Console.WriteLine($"Слов: {CountWords(text)}");

static string Capitalize(string word)
{
    if (word.Length == 0)
    {
        return "";
    }
    return word.Substring(0, 1).ToUpper() + word.Substring(1).ToLower();
}

static int CountWords(string text)
{
    string[] parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    return parts.Length;
}
