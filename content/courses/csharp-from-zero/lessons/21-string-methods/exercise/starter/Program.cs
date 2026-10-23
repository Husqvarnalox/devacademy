string name = Console.ReadLine() ?? "";
string text = Console.ReadLine() ?? "";

Console.WriteLine($"Имя: [{Capitalize(name.Trim())}]");
Console.WriteLine($"Слов: {CountWords(text)}");

static string Capitalize(string word)
{
    // TODO: верните слово с заглавной первой буквой и строчными остальными ("аННА" -> "Анна");
    //       для пустой строки верните пустую строку
    return word;
}

static int CountWords(string text)
{
    // TODO: верните количество слов в text (слова разделены одним или несколькими пробелами)
    return 0;
}
