<!-- section: simple -->
## Что проверяет эта проверка

Чтение ввода через `Console.ReadLine()` и конец ввода (`null`), `int.Parse` и `int.TryParse`, `FormatException`, `try / catch / finally`, строгий разбор через `NumberStyles` и `InvariantCulture`, подсчёт неверных записей.

## Что даёт прохождение

Проверка состоит из короткого упражнения с запуском тестов и квиза на чтение кода (порог квиза - 75%). Если пройдены обе части, засчитываются уроки: [Ввод с клавиатуры: Console.ReadLine](lesson:csharp.from-zero.reading-input), [Числа из ввода: Parse](lesson:csharp.from-zero.parsing-numbers), [Исключения и TryParse](lesson:csharp.from-zero.exceptions).

Если не пройдены, ничего не отнимается: прогресс сохраняется, а система покажет первый незавершённый урок из этого списка как место для повторения. Проверку можно пройти снова в любой момент, и открыть её можно без каких-либо условий.

<!-- section: examples -->
## Пример того, что понадобится

```csharp run
string[] lines = { "Anna 90", "Boris x", "Clara 75", "Dan" };
int valid = 0;
int invalid = 0;
foreach (string line in lines)
{
    string[] parts = line.Split(' ');
    if (parts.Length == 2 && int.TryParse(parts[1], out int score))
    {
        valid++;
        Console.WriteLine($"{parts[0]}: {score}");
    }
    else
    {
        invalid++;
    }
}
Console.WriteLine($"{valid} {invalid}");
try
{
    Console.WriteLine(int.Parse("x"));
}
catch (FormatException)
{
    Console.WriteLine("ошибка формата");
}
```
```output
Anna: 90
Clara: 75
2 2
ошибка формата
```

Строка делится на части, и запись считается верной только если частей две, а вторая разбирается через `TryParse`. Неверные записи считаются, но программа не падает. Метод `int.Parse` на неверной строке бросает `FormatException`, который можно перехватить через `catch`.

<!-- section: precise -->
## Что нужно уметь

* читать строки до конца ввода (`ReadLine` возвращает `null`) и пропускать пустые;
* выбирать между `Parse` с `try/catch` и `TryParse` и знать, что `TryParse` при неудаче возвращает `false` и `0`;
* знать порядок блоков `try`, `catch` и `finally`;
* задавать строгий формат разбора через `NumberStyles` и `CultureInfo.InvariantCulture`;
* проверять диапазон после успешного разбора;
* не делить на ноль и не вычислять среднее пустого списка.

<!-- section: mistakes -->
## На чём чаще всего ошибаются

* Вызов `int.Parse` на пользовательском вводе без перехвата: одно плохое значение останавливает программу.
* `TryParse` без стилей принимает пробелы, знак `+` и разделитель тысяч в `decimal`.
* Забыли присвоить `out`-параметр на одном из путей: ошибка компиляции `CS0177`.
* Среднее пустого списка: деление на ноль или `NaN`.
* Ожидание, что `ReadLine` вернёт пустую строку в конце ввода: возвращается `null`.
* Culture по умолчанию: `double.Parse("2.5")` на русской системе бросает `FormatException`.

<!-- section: summary -->
## Итог

Если задания показались трудными, начните с уроков из списка: [Ввод с клавиатуры: Console.ReadLine](lesson:csharp.from-zero.reading-input), [Числа из ввода: Parse](lesson:csharp.from-zero.parsing-numbers), [Исключения и TryParse](lesson:csharp.from-zero.exceptions). После повторения вернитесь к проверке: результат не зависит от числа попыток, а засчитанное остаётся засчитанным.
