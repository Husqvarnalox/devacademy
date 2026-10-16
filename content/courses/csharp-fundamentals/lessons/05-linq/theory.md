<!-- section: simple -->
## Запросы к последовательностям

**LINQ** (Language Integrated Query) - набор методов расширения (`Where`, `Select`, `GroupBy`, `OrderBy`, `Join` ...) над `IEnumerable<T>`. Вы описываете, **что** нужно получить из последовательности, а не как перебирать её циклами: `numbers.Where(x => x % 2 == 0).Select(x => x * x)`.

Главная идея, без которой LINQ путает: запрос - это **рецепт, а не результат**. Строка `var q = items.Where(...)` ещё ничего не вычислила. Работа начинается, когда кто-то начинает перебирать `q` (`foreach`, `ToList()`, `Count()`, `First()`). Это **отложенное выполнение** (deferred execution). Каждый новый перебор запускает рецепт заново - отсюда главная ловушка, **множественное перечисление**.

У запросов два синтаксиса: **методный** (`.Where(...).Select(...)`) и **запросный** (`from x in xs where ... select ...`). Запросный синтаксис - сахар, который компилятор превращает в вызовы методов; выбирайте тот, что читается лучше (часто `join` и `let` приятнее в запросном виде).

<!-- section: precise -->
## Точные правила

**Отложенное и немедленное выполнение.**

* Операторы, **возвращающие последовательность** (`Where`, `Select`, `Take`, `Skip`, `OrderBy`, `GroupBy`, `Join`, `SelectMany`, `Concat`, `Distinct`), выполняются отложенно: при вызове создаётся только итератор. Исключение при проверке аргумента (например, `null` вместо источника) бросается сразу, а ошибки внутри лямбд - только при переборе.
* Операторы, **возвращающие значение или коллекцию** (`ToList`, `ToArray`, `ToDictionary`, `ToLookup`, `Count`, `Sum`, `First`, `Any`, `Aggregate`), выполняются немедленно: они запускают перебор.
* Потоковые операторы (`Where`, `Select`, `Take`) отдают элементы по одному и могут остановиться раньше конца источника. Буферизующие (`OrderBy`, `GroupBy`, `Join` по второй последовательности, `Reverse`) при первом запросе элемента читают **весь** источник.
* `First`, `Any`, `Take(k)` останавливают перебор сразу после нужного элемента (**short-circuit**), поэтому `Any()` выгоднее `Count() > 0`.

**Захват переменных.** Лямбда в запросе захватывает *переменную*, а не её значение. Если изменить переменную после построения запроса, но до перебора, запрос увидит новое значение. Источник тоже читается в момент перебора, а не построения.

**Множественное перечисление.** Если один и тот же `IEnumerable<T>`-запрос перебирается дважды, вычисляется дважды: повторяются побочные эффекты, чтение файла/базы, дорогие вычисления; результаты могут и отличаться. Решение - материализовать результат один раз (`ToList()`/`ToArray()`) или принимать `IReadOnlyCollection<T>`.

**Группировка и соединения.**

* `GroupBy(key)` возвращает `IEnumerable<IGrouping<TKey, T>>`: каждая группа - это последовательность элементов с общим `Key`; порядок групп - порядок первого появления ключа.
* `ToLookup(key)` строит `ILookup<TKey,T>` **сразу** (немедленно) и не изменяется потом; индексатор `lookup[key]` возвращает пустую последовательность для отсутствующего ключа (а не бросает исключение, как `Dictionary`) - удобно для многократного поиска по ключу за `O(1)`.
* `Join(inner, outerKey, innerKey, result)` - внутреннее соединение (как `INNER JOIN`); `GroupJoin` - соединение с группировкой (основа левого соединения); `SelectMany` - «разворачивание»: из каждого элемента получает последовательность и склеивает их.
* `OrderBy`/`ThenBy` - **устойчивая** сортировка; для строк передавайте явный `StringComparer`, иначе порядок зависит от культуры.

**Запросный синтаксис.** `from x in xs where p orderby k descending select r` транслируется в `xs.Where(...).OrderByDescending(...).Select(...)`. Ключевые слова: `from`, `where`, `select`, `orderby`, `group ... by`, `join ... on ... equals`, `let`, `into`.

**Производительность.**

* Каждый оператор - вложенный итератор и делегат: накладные расходы малы, но в горячих циклах заметны. Для массивов и списков в самых критичных местах иногда проще цикл.
* Не повторяйте дорогое: `list.Where(...).Count()` лучше `list.Count(...)`; проверку наличия - через `Any()`.
* Многократные поиски по ключу в цикле - через `ToLookup`/`ToDictionary`/`HashSet`, а не `Where(...).First()` внутри цикла (иначе `O(n*m)`).
* `OrderBy(...).First()` сортирует всё; для минимума используйте `MinBy`/`MaxBy`.

<!-- section: examples -->
## Примеры

**Пример 1. Запрос - это рецепт.**

```csharp run
using System;
using System.Collections.Generic;
using System.Linq;

var log = new List<string>();
IEnumerable<int> query = new[] { 1, 2, 3, 4 }
    .Where(x => { log.Add($"w{x}"); return x % 2 == 0; })
    .Select(x => { log.Add($"s{x}"); return x * x; });

Console.WriteLine($"построен, вычислений: {log.Count}");
Console.WriteLine(string.Join(",", query));
Console.WriteLine(string.Join(" ", log));

log.Clear();
int first = query.First();
Console.WriteLine($"First = {first}, журнал: {string.Join(" ", log)}");

// источник и захваченная переменная читаются при переборе
var source = new List<int> { 1, 2, 3 };
int factor = 10;
var scaled = source.Select(x => x * factor);
source.Add(4);
factor = 100;
Console.WriteLine(string.Join(",", scaled));
```
```output
построен, вычислений: 0
4,16
w1 w2 s2 w3 w4 s4
First = 4, журнал: w1 w2 s2
100,200,300,400
```

Фильтр и проекция чередуются для каждого элемента (`w1 w2 s2 w3 w4 s4`), `First()` остановился после первого подходящего (`w1 w2 s2`), а запрос `scaled` увидел и добавленный элемент `4`, и новое значение `factor`.

**Пример 2. Множественное перечисление.**

```csharp run
using System;
using System.Collections.Generic;
using System.Linq;

int evaluations = 0;
IEnumerable<int> Numbers()
{
    for (int i = 1; i <= 3; i++)
    {
        evaluations++;
        yield return i;
    }
}

IEnumerable<int> query = Numbers().Where(x => x > 1);
Console.WriteLine($"{query.Count()} {query.Sum()} {query.Max()}");
Console.WriteLine($"вычислений источника: {evaluations}");

evaluations = 0;
List<int> snapshot = Numbers().Where(x => x > 1).ToList();
Console.WriteLine($"{snapshot.Count} {snapshot.Sum()} {snapshot.Max()}");
Console.WriteLine($"вычислений источника: {evaluations}");
```
```output
2 5 3
вычислений источника: 9
2 5 3
вычислений источника: 3
```

Три терминальных оператора прошли источник три раза (9 шагов вместо 3). `ToList()` материализует результат один раз.

**Пример 3. Группировка, соединения и запросный синтаксис.**

```csharp run
using System;
using System.Linq;

Person[] people = [new("Ann", "dev", 30), new("Bob", "dev", 25), new("Cy", "ops", 41)];
Role[] roles = [new("dev", "Code"), new("ops", "Run")];

// методный и запросный синтаксис - один и тот же запрос
var a = people.Where(p => p.Age > 26).OrderByDescending(p => p.Name).Select(p => p.Name);
var b = from p in people where p.Age > 26 orderby p.Name descending select p.Name;
Console.WriteLine($"{string.Join(",", a)} | {string.Join(",", b)}");

foreach (var g in people.GroupBy(p => p.Role))
    Console.WriteLine($"{g.Key}: {g.Count()} чел., сумма возрастов {g.Sum(p => p.Age)}");

var lookup = people.ToLookup(p => p.Role);
Console.WriteLine($"dev={lookup["dev"].Count()} zzz={lookup["zzz"].Count()}");

Console.WriteLine(string.Join(", ", people.Join(roles, p => p.Role, r => r.Key, (p, r) => $"{p.Name}-{r.Title}")));
Console.WriteLine(string.Join(", ", roles.GroupJoin(people, r => r.Key, p => p.Role, (r, ps) => $"{r.Title}:{ps.Count()}")));
Console.WriteLine(string.Join(",", new[] { "a b", "c d e" }.SelectMany(s => s.Split(' '))));

record Person(string Name, string Role, int Age);
record Role(string Key, string Title);
```
```output
Cy,Ann | Cy,Ann
dev: 2 чел., сумма возрастов 55
ops: 1 чел., сумма возрастов 41
dev=2 zzz=0
Ann-Code, Bob-Code, Cy-Run
Code:2, Run:1
a,b,c,d,e
```

<!-- section: mistakes -->
## Типичные ошибки

* **Перебирать один запрос несколько раз.** `Count()`, затем `Sum()`, затем `foreach` - три прохода и три вычисления источника; материализуйте через `ToList()`.
* **Считать запрос результатом.** `var q = list.Where(...)` не копирует данные; если `list` изменится, `q` это увидит. Чтобы зафиксировать состояние, вызовите `ToList()`.
* **Побочные эффекты в `Select`/`Where`.** Они выполнятся неизвестное число раз (0, 1 или несколько); для действий используйте `foreach`.
* **Возвращать отложенный запрос, который ссылается на закрытый ресурс.** Если метод открыл файл/контекст в `using` и вернул `Select(...)` без `ToList()`, перебор случится после закрытия.
* **Захват изменяемой переменной.** Запрос увидит значение на момент перебора, а не построения.
* **`Count() > 0` вместо `Any()`.** Для общего `IEnumerable<T>` первое перебирает всё.
* **`OrderBy(...).First()` вместо `MinBy`/`MaxBy`.** Лишняя сортировка за `O(n log n)`.
* **`Where(...).First()` внутри цикла по другой коллекции.** Квадратичное время; постройте `ToLookup`/`ToDictionary` один раз.
* **Сравнивать строки без компаратора в `OrderBy`/`GroupBy`.** Порядок и совпадение ключей зависят от настроек; задавайте `StringComparer.Ordinal`/`OrdinalIgnoreCase`.
* **`First()` на пустой последовательности.** `InvalidOperationException` («Sequence contains no elements»); используйте `FirstOrDefault` или `Any`.

<!-- section: summary -->
## Итоги

* LINQ-запрос - рецепт: операторы-последовательности откладываются, операторы-значения (`ToList`, `Count`, `First`) запускают вычисление.
* Каждый перебор повторяет вычисление; результат, нужный несколько раз, материализуйте.
* Лямбды захватывают переменные, источник читается при переборе.
* `GroupBy` - группы, `ToLookup` - немедленный индекс по ключу, `Join`/`GroupJoin` - соединения, `SelectMany` - разворачивание.
* Методный и запросный синтаксисы равноправны; сортировки устойчивы, строки сравнивайте с явным компаратором.
* Для горячих мест: `Any` вместо `Count() > 0`, `MaxBy` вместо сортировки, индекс по ключу вместо вложенного поиска.

Сравните с [массивами и итерацией в JavaScript](lesson:javascript.fundamentals.arrays-and-iteration). Дальше: [исключения и ресурсы](lesson:csharp.fundamentals.exceptions-and-resources).

<!-- section: facts -->
## Факты

* Запросные выражения компилятор превращает в вызовы методов расширения (`Where`, `Select`, `OrderBy`...), поэтому они дают тот же результат, что и методный синтаксис.
* Методы `Enumerable.OrderBy` и `ThenBy` выполняют устойчивую сортировку, а `GroupBy` и `Join` возвращают отложенные запросы.
