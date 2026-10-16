<!-- section: simple -->
## Один код для многих типов

**Обобщения (generics)** позволяют написать тип или метод один раз и использовать с любым типом, сохранив проверки компилятора: `List<int>` хранит только `int`, `List<string>` - только `string`, и никаких приведений и упаковки. Параметр типа (`T`) - как параметр метода, только для типов.

**Ограничения** (`where T : ...`) говорят компилятору, что умеет `T`. Без ограничения с `T` можно делать очень мало; с `where T : IComparable<T>` появляется `CompareTo`.

**Вариантность** отвечает на вопрос: можно ли использовать `IEnumerable<string>` там, где нужен `IEnumerable<object>`. Для безопасных случаев (только чтение) - да (`out`), для приёма значений - наоборот (`in`), для остальных - нет.

**Коллекции** - готовые структуры данных: `List<T>` (массив с ростом), `Dictionary<TKey,TValue>` (хеш-таблица), `HashSet<T>` (множество), `SortedDictionary`, `Queue`, `Stack`, `PriorityQueue`. Выбор определяется операциями, которые вы делаете чаще всего, - то есть их **сложностью**. И последнее: что принимать и возвращать - `IEnumerable<T>` (можно только перебрать) или `IReadOnlyList<T>` (есть `Count` и индексатор).

<!-- section: precise -->
## Точные правила

**Обобщённые типы и методы.**

* `List<T>` - *открытый* обобщённый тип; `List<int>` - *закрытый* сконструированный тип. `typeof(List<int>) != typeof(List<string>)` - это разные типы во время выполнения (в отличие от Java, обобщения в .NET **не стираются**).
* Для значимых аргументов типа среда выполнения создаёт отдельный машинный код (поэтому нет упаковки); для ссылочных - общий код для всех.
* Вывод типов: `Max(3, 7)` - компилятор выводит `T = int`; `Max<int>(3, 7)` - явно.
* `default(T)`, `new T[]`, `typeof(T)`, `is T` работают с любым `T`; сравнение `a > b` для неограниченного `T` не компилируется (CS0019).

**Ограничения.**

| Ограничение | Смысл | Нарушение |
| --- | --- | --- |
| `where T : struct` | значимый тип (не `Nullable`) | CS0453 |
| `where T : class` | ссылочный тип | CS0452 |
| `where T : notnull` | не nullable | предупреждение |
| `where T : new()` | есть открытый конструктор без параметров | CS0304 при `new T()` без ограничения |
| `where T : Base` / `IFoo` | наследник/реализует | CS0311 |
| `where T : INumber<T>` | числовые операции (generic math, C# 11) | - |
| `where T : allows ref struct` | допускает `Span<T>` (C# 13) | - |

**Вариантность** (только для интерфейсов и делегатов).

* **Ковариантность** `out T`: `T` встречается только в выходных позициях. `IEnumerable<out T>`, `IReadOnlyList<out T>`, `Func<out TResult>`. Поэтому `IEnumerable<string>` присваивается `IEnumerable<object>`.
* **Контравариантность** `in T`: `T` встречается только во входных позициях. `Action<in T>`, `IComparer<in T>`, `IEqualityComparer<in T>`. `Action<object>` присваивается `Action<string>`.
* Вариантность работает только для **ссылочных** аргументов типа. `List<T>` инвариантен: `List<object> = new List<string>()` - CS0029.
* Массивы ковариантны **небезопасно**: `object[] oarr = new string[1]; oarr[0] = 1;` компилируется и бросает `ArrayTypeMismatchException` во время выполнения.
* Нарушение правил позиций в своём интерфейсе - CS1961.

**Сложность основных коллекций** (по документации; сложность хеш-структур - в среднем, при хорошем хеш-коде).

| Структура | Поиск/доступ | Добавление | Удаление/вставка в середину |
| --- | --- | --- | --- |
| `List<T>` | по индексу `O(1)`; `Contains` - `O(n)` | в конец `O(1)` (амортизированно; при росте `O(n)`) | `Insert`, `Remove` - `O(n)` |
| `Dictionary<K,V>` | `O(1)` | `O(1)` (при росте `O(n)`) | `O(1)` |
| `HashSet<T>` | `Contains` - `O(1)` | `O(1)` | `O(1)` |
| `SortedDictionary<K,V>` | `O(log n)` | `O(log n)` | `O(log n)`, ключи в порядке сортировки |
| `LinkedList<T>` | поиск `O(n)` | `AddFirst/AddLast` - `O(1)` | `O(1)` по узлу |

Порядок перебора `Dictionary` и `HashSet` не гарантируется контрактом; если нужен порядок - сортируйте результат или используйте `SortedDictionary`/`SortedSet`.

**Выражения коллекций (C# 12).** `[1, 2, 3]` создаёт массив, `List<T>`, `ImmutableArray<T>`, `Span<T>` и другие типы по целевому типу: `List<int> a = [1, 2, 3];`. Оператор распространения `..` встраивает другую последовательность: `[.. a, 4, .. new[] { 5, 6 }]`. Пустая коллекция - `[]`.

**`IEnumerable<T>` и `IReadOnlyList<T>`.**

* `IEnumerable<T>` - только последовательный перебор, возможно отложенный и многократно вычисляемый (см. урок про [LINQ](lesson:csharp.fundamentals.linq)). `Count()` на нём - это перебор, а не свойство.
* `IReadOnlyList<T>` добавляет `Count` и индексатор; `IReadOnlyCollection<T>` - только `Count`.
* Это **только чтение через интерфейс**, а не неизменяемость: если за ним стоит `List<T>`, изменения исходного списка видны через интерфейс. Для настоящей неизменяемости есть `ImmutableArray<T>`, `ImmutableList<T>` и `System.Collections.Frozen`.
* Принимайте самый узкий нужный интерфейс (`IEnumerable<T>`, если достаточно перебора), возвращайте самый информативный (`IReadOnlyList<T>` или конкретный тип).
* Изменение коллекции во время `foreach` по ней - `InvalidOperationException` на следующей итерации.

<!-- section: examples -->
## Примеры

**Пример 1. Обобщённые методы и ограничения.**

```csharp run
using System;
using System.Collections.Generic;
using System.Numerics;

Console.WriteLine(Max(3, 7));
Console.WriteLine(Max("pear", "apple"));
Console.WriteLine(Sum([1, 2, 3]));
Console.WriteLine(Sum([1.5, 2.5]));
Console.WriteLine(Create<List<int>>().Count);

var pair = new Pair<int, string>(1, "x");
Console.WriteLine(pair.Swap());

Console.WriteLine(typeof(List<int>) == typeof(List<string>));
Console.WriteLine($"{typeof(List<>).IsGenericTypeDefinition} {typeof(List<int>).IsConstructedGenericType}");

static T Max<T>(T a, T b) where T : IComparable<T> => a.CompareTo(b) >= 0 ? a : b;

static T Sum<T>(IEnumerable<T> items) where T : INumber<T>
{
    T total = T.Zero;
    foreach (T item in items) total += item;
    return total;
}

static T Create<T>() where T : new() => new T();

record Pair<A, B>(A First, B Second)
{
    public Pair<B, A> Swap() => new(Second, First);
}
```
```output
7
pear
6
4
0
Pair { First = x, Second = 1 }
False
True True
```

Один метод `Sum` работает для `int` и `double`: ограничение `INumber<T>` даёт операторы и `T.Zero`. `Max("pear", "apple")` сравнивает строки через `IComparable<string>`.

**Пример 2. Вариантность.**

```csharp run
using System;
using System.Collections.Generic;

IEnumerable<string> strings = new List<string> { "a", "b" };
IEnumerable<object> objects = strings;           // ковариантность (out)
Console.WriteLine(objects.Count());

Action<object> printObject = o => Console.WriteLine("object: " + o);
Action<string> printString = printObject;       // контравариантность (in)
printString("text");

// List<object> bad = new List<string>();       // CS0029: List<T> инвариантен

string[] names = ["x", "y"];
object[] asObjects = names;                      // ковариантность массивов компилируется...
try
{
    asObjects[0] = 42;                           // ...но проверяется только во время выполнения
}
catch (ArrayTypeMismatchException)
{
    Console.WriteLine("ArrayTypeMismatchException");
}
```
```output
2
object: text
ArrayTypeMismatchException
```

Ковариантность `IEnumerable<out T>` безопасна, потому что через неё можно только читать. Ковариантность массивов унаследована из ранних версий и проверяется во время выполнения.

**Пример 3. Выбор коллекции по операциям.**

```csharp run
using System;
using System.Collections.Generic;
using System.Linq;

string text = "the quick brown fox jumps over the lazy dog the end";

var counts = new Dictionary<string, int>();
foreach (string word in text.Split(' '))
    counts[word] = counts.GetValueOrDefault(word) + 1;     // O(1) на слово
foreach (var (word, count) in counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).Take(3))
    Console.WriteLine($"{word} {count}");

var seen = new HashSet<string>();
var firstRepeat = text.Split(' ').First(w => !seen.Add(w));   // Add вернул false: уже было
Console.WriteLine(firstRepeat);

var sorted = new SortedDictionary<string, int>(counts);       // ключи по алфавиту
Console.WriteLine(string.Join(",", sorted.Keys.Take(3)));

var queue = new Queue<string>(["a", "b", "c"]);               // FIFO
queue.Enqueue("d");
Console.WriteLine(queue.Dequeue() + queue.Peek());

var priority = new PriorityQueue<string, int>();
priority.Enqueue("low", 5);
priority.Enqueue("urgent", 1);
Console.WriteLine(priority.Dequeue());                        // наименьший приоритет первым
```
```output
the 3
brown 1
dog 1
the
brown,dog,end
ab
urgent
```

`HashSet.Add` возвращает `false`, если элемент уже есть: так находят первый повтор за один проход. Порядок вывода `Dictionary` мы не использовали: результат сортируется явно.

**Пример 4. Выражения коллекций и `IReadOnlyList`.**

```csharp run
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

List<int> list = [1, 2, 3];
int[] extra = [10, 20];
List<int> merged = [.. list, 4, .. extra];     // распространение
Console.WriteLine(string.Join(",", merged));

int[] empty = [];
Console.WriteLine(empty.Length);

IReadOnlyList<int> view = list;                // только чтение через интерфейс
IEnumerable<int> sequence = list;
list.Add(99);
Console.WriteLine($"{view.Count} {view[3]} {sequence.Count()}");   // исходный список изменился

ImmutableList<int> snapshot = list.ToImmutableList();
list.Add(100);
Console.WriteLine($"{snapshot.Count} {list.Count}");

try
{
    foreach (int x in list)
        if (x == 2) list.Add(0);                // изменение во время перебора
}
catch (InvalidOperationException e)
{
    Console.WriteLine(e.Message);
}
```
```output
1,2,3,4,10,20
0
4 99 4
4 5
Collection was modified; enumeration operation may not execute.
```

`IReadOnlyList` не защищает данные: `view` и `sequence` показывают изменения исходного списка. Моментальный снимок - `ImmutableList`.

**Пример 5. Ошибки компиляции (без запуска).**

```csharp
List<object> lo = new List<string>();                // CS0029: List<T> инвариантен

static T Zero<T>() where T : struct => default;
var s = Zero<string>();                              // CS0453: string - не значимый тип

static bool Greater<T>(T a, T b) => a > b;           // CS0019: у неограниченного T нет оператора >

interface IBad<out T> { void Set(T value); }         // CS1961: T в позиции ввода нельзя объявить out
```

<!-- section: mistakes -->
## Типичные ошибки

* **Строить логику на порядке `Dictionary` и `HashSet`.** Он не гарантируется; сортируйте явно.
* **`List.Contains` в цикле.** `O(n)` внутри цикла даёт `O(n^2)`; для проверок принадлежности используйте `HashSet`.
* **`ContainsKey` и затем индексатор.** Два поиска; применяйте `TryGetValue`, `GetValueOrDefault`, `TryAdd`.
* **Изменять коллекцию в `foreach`.** `InvalidOperationException`; перебирайте копию (`ToList()`) или собирайте изменения отдельно.
* **Принимать `List<T>`, где достаточно `IEnumerable<T>`.** Лишняя связанность и невозможность передать массив или запрос.
* **Считать `IReadOnlyList<T>` неизменяемым.** Это лишь ограниченный вид на изменяемую коллекцию.
* **Вызывать `Count()` на `IEnumerable<T>` многократно.** Это каждый раз перебор (а для отложенных запросов - повторное вычисление).
* **Ковариантные массивы.** Присваивание через `object[]` может упасть во время выполнения; используйте обобщённые коллекции.
* **Ключи словаря без согласованных `Equals`/`GetHashCode`.** См. урок про [контракты равенства](lesson:csharp.fundamentals.interfaces-inheritance-polymorphism).
* **Избыточные ограничения.** `where T : class` там, где достаточно интерфейса, лишает вызывающего возможности подставить структуру.

<!-- section: summary -->
## Итоги

* Обобщения .NET сохраняются во время выполнения, дают безопасность типов и отсутствие упаковки для значимых типов.
* Ограничения `where` сообщают компилятору возможности `T`; `INumber<T>` позволяет писать числовой код один раз.
* `out` - ковариантность (только чтение), `in` - контравариантность (только приём); `List<T>` инвариантен; массивы ковариантны небезопасно.
* Выбирайте коллекцию по операциям: `List` - индекс и добавление в конец, `Dictionary`/`HashSet` - поиск за `O(1)`, `Sorted*` - порядок и `O(log n)`.
* Выражения коллекций (C# 12) и `..` упрощают создание; `IReadOnlyList<T>` - только чтение, а не неизменяемость.

Сравните со [срезами и картами в Go](lesson:go.fundamentals.maps-and-slices-deep-dive). Дальше: [LINQ](lesson:csharp.fundamentals.linq).

<!-- section: facts -->
## Факты

* Обобщения в CLR появились в .NET Framework 2.0 и C# 2.0; информация о параметрах типа сохраняется во время выполнения, поэтому `typeof(List<int>)` и `typeof(List<string>)` различаются.
* Выражения коллекций и первичные конструкторы появились в C# 12; `allows ref struct` - в C# 13.
