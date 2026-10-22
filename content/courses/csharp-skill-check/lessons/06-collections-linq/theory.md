<!-- section: simple -->
## Что проверяет эта проверка

Обобщённые коллекции `List<T>` и `Dictionary<TKey, TValue>`, методы LINQ (`Where`, `Select`, `OrderBy`, `GroupBy`, `Take`, `Sum`), отложенное и немедленное выполнение, `FirstOrDefault` и `TryGetValue`.

## Что даёт прохождение

Проверка состоит из короткого упражнения с запуском тестов и квиза на чтение кода (порог квиза - 75%). Если пройдены обе части, засчитываются уроки: [Обобщения и коллекции](lesson:csharp.fundamentals.generics-and-collections), [LINQ](lesson:csharp.fundamentals.linq).

Если не пройдены, ничего не отнимается: прогресс сохраняется, а система покажет первый незавершённый урок из этого списка как место для повторения. Проверку можно пройти снова в любой момент, и открыть её можно без каких-либо условий.

<!-- section: examples -->
## Пример того, что понадобится

```csharp run
List<int> nums = new List<int> { 5, 2, 8, 1, 9, 4 };
IEnumerable<int> big = nums.Where(n => n > 3);
nums.Add(10);
Console.WriteLine(string.Join(" ", big));
Console.WriteLine(string.Join(" ", nums.OrderBy(n => n).Take(3)));
Console.WriteLine($"{nums.Sum()} {nums.Max()}");
Console.WriteLine(new List<int>().FirstOrDefault());

Dictionary<string, int> stock = new Dictionary<string, int> { ["apple"] = 3 };
Console.WriteLine(stock.TryGetValue("pear", out int count) ? count : -1);

IEnumerable<string> groups = new[] { "a", "bb", "cc", "d" }
    .GroupBy(w => w.Length)
    .Select(g => $"{g.Key}:{g.Count()}");
Console.WriteLine(string.Join(",", groups));
```
```output
5 8 9 4 10
1 2 4
39 10
0
-1
1:2,2:2
```

Запрос `Where` не выполняется при объявлении, а срабатывает при переборе: поэтому 10, добавленное позже, попадает в результат. `OrderBy` и `Take` возвращают новую последовательность, а исходный список не меняют. `FirstOrDefault` для пустого списка даёт `0`, `TryGetValue` сообщает об отсутствии ключа без исключения, а `GroupBy` собирает группы в порядке первого появления ключа.

<!-- section: precise -->
## Что нужно уметь

* выбирать между `List<T>` и `Dictionary<TKey, TValue>` и читать из словаря через `TryGetValue`;
* строить запросы `Where`, `Select`, `OrderBy`/`ThenBy`, `GroupBy`, `Take`, `Sum`, `Max`;
* помнить, что запросы LINQ выполняются отложенно, а `ToList()` и агрегаты (`Sum`, `Count`) - сразу;
* знать, что `First` бросает исключение на пустой последовательности, а `FirstOrDefault` возвращает значение по умолчанию;
* передавать `StringComparer.Ordinal`, когда порядок строк не должен зависеть от культуры системы;
* не менять коллекцию во время `foreach`.

<!-- section: mistakes -->
## На чём чаще всего ошибаются

* Ожидание, что запрос LINQ посчитан в момент объявления: он видит позднейшие изменения исходной коллекции.
* Изменение списка внутри `foreach`: `InvalidOperationException`.
* `FirstOrDefault` для чисел: результат `0` не отличить от настоящего нуля.
* Чтение `dictionary[key]` для отсутствующего ключа: `KeyNotFoundException`.
* Сортировка строк без указания сравнения: порядок зависит от региональных настроек.
* Повторное перечисление дорогого запроса: сохраните результат в `ToList()`.

<!-- section: summary -->
## Итог

Если задания показались трудными, начните с уроков из списка: [Обобщения и коллекции](lesson:csharp.fundamentals.generics-and-collections), [LINQ](lesson:csharp.fundamentals.linq). После повторения вернитесь к проверке: результат не зависит от числа попыток, а засчитанное остаётся засчитанным.
