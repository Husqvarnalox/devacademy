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
    // TODO: НОВЫЙ массив накопленных сумм: для 1 2 3 4 получится 1 3 6 10; values не менять
    return new int[0];
}

static string MostCommonWord(string text)
{
    // TODO: самое частое слово (слова разделены пробелами, регистр не важен, результат строчными);
    //       при равенстве - меньшее по StringComparer.Ordinal; для пустого текста - ""
    return "?";
}

static List<string> Unique(List<string> items)
{
    // TODO: НОВЫЙ список без повторов, отсортированный по StringComparer.Ordinal; items не менять
    return new List<string>();
}

static bool IsPalindrome(string text)
{
    // TODO: читается ли текст одинаково в обе стороны, если учитывать только буквы (char.IsLetter) без учёта регистра
    return false;
}
