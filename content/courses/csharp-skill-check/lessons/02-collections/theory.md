<!-- section: simple -->
## Что проверяет эта проверка

Массивы фиксированной длины и индексы, `List<T>` (`Add`, `RemoveAt`, `Sort`), `Dictionary<TKey, TValue>` и подсчёт частот, `HashSet<T>`, `foreach`, методы строк `Split`, `Substring`, `ToUpper`, `Join`, символы `char`.

## Что даёт прохождение

Проверка состоит из короткого упражнения с запуском тестов и квиза на чтение кода (порог квиза - 75%). Если пройдены обе части, засчитываются уроки: [Массивы](lesson:csharp.from-zero.arrays), [Списки List](lesson:csharp.from-zero.lists), [Словари Dictionary](lesson:csharp.from-zero.dictionaries), [Методы строк](lesson:csharp.from-zero.string-methods).

Если не пройдены, ничего не отнимается: прогресс сохраняется, а система покажет первый незавершённый урок из этого списка как место для повторения. Проверку можно пройти снова в любой момент, и открыть её можно без каких-либо условий.

<!-- section: examples -->
## Пример того, что понадобится

```csharp run
var counts = new Dictionary<string, int>();
foreach (string word in "to be or not to be".Split(' '))
{
    counts[word] = counts.GetValueOrDefault(word) + 1;
}
var keys = new List<string>(counts.Keys);
keys.Sort(StringComparer.Ordinal);
foreach (string key in keys)
{
    Console.WriteLine($"{key}: {counts[key]}");
}

int[] data = { 4, 8, 15 };
int[] copy = data;
copy[0] = 0;
Console.WriteLine(string.Join(",", data));
Console.WriteLine(string.Join("|", "2024-05-17".Split('-')));
Console.WriteLine("Hello".Substring(1, 3).ToUpper());
```
```output
be: 2
not: 1
or: 1
to: 2
0,8,15
2024|05|17
ELL
```

Словарь копит частоты слов, а ключи для вывода копируются в список и сортируются по `StringComparer.Ordinal`: порядок обхода самого словаря не гарантирован. Присваивание `copy = data` не копирует массив, а даёт второе имя того же массива. `Split('-')` разделяет строку, `Join` склеивает, `Substring(1, 3)` берёт три символа с индекса 1.

<!-- section: precise -->
## Что нужно уметь

* создавать и заполнять массивы и списки, знать, что индексы начинаются с нуля;
* считать частоты в словаре через `TryGetValue` или `GetValueOrDefault`;
* убирать повторы через `HashSet<T>` и сортировать списки с `StringComparer.Ordinal`;
* не менять коллекцию внутри `foreach` и не путать копию с ссылкой;
* разбирать строку через `Split`, `Trim`, `Substring`, проверять символы через `char.IsLetter`;
* возвращать новую коллекцию, если по условию вход менять нельзя.

<!-- section: mistakes -->
## На чём чаще всего ошибаются

* Присваивание массива или списка вместо копирования: оба имени указывают на один объект.
* Чтение `dict[key]` для отсутствующего ключа: бросается `KeyNotFoundException`.
* Выход за границы массива: индекс `Length` уже вне диапазона.
* Сортировка строк без `StringComparer.Ordinal`: порядок зависит от культуры компьютера.
* Изменение списка в `foreach`: `InvalidOperationException`.
* `Split` с пустыми элементами: после двойного разделителя остаётся пустая строка.

<!-- section: summary -->
## Итог

Если задания показались трудными, начните с уроков из списка: [Массивы](lesson:csharp.from-zero.arrays), [Списки List](lesson:csharp.from-zero.lists), [Словари Dictionary](lesson:csharp.from-zero.dictionaries), [Методы строк](lesson:csharp.from-zero.string-methods). После повторения вернитесь к проверке: результат не зависит от числа попыток, а засчитанное остаётся засчитанным.
