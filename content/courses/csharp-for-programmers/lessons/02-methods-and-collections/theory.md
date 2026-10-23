<!-- section: simple -->
## Зачем этот урок

Методы и коллекции в C# выглядят привычно, но у них есть особенности, которые стоят времени отладки: массив и список - **ссылочные** типы, поэтому присваивание копирует ссылку, а `==` для массивов сравнивает ссылки; чтение отсутствующего ключа словаря бросает исключение; изменение списка внутри `foreach` запрещено; параметры методов бывают необязательными, именованными и выходными (`out`). Успешное прохождение (упражнение и квиз) засчитывает уроки 16-21 курса «C# с нуля».

## Главная мысль

Переменная массива, списка и словаря хранит **ссылку** на объект. Метод получает копию ссылки: он может изменить содержимое переданной коллекции, но не может подменить переменную вызывающего. Поэтому каждый метод должен определённо заявлять, меняет ли он вход или возвращает новую коллекцию, и это стоит проверять в тестах.

<!-- section: examples -->
## Пример 1. Методы: параметры по умолчанию, params, out, кортежи

Методы пишутся как локальные функции в файле с инструкциями верхнего уровня; здесь они объявлены с `static`, чтобы не захватывать переменные снаружи.

```csharp run
static int Square(int x) => x * x;

static string Label(string name, int count = 1, string suffix = "шт.")
{
    return $"{name}: {count} {suffix}";
}

static int Sum(params int[] values)
{
    int total = 0;
    foreach (int v in values)
    {
        total += v;
    }
    return total;
}

static (int Min, int Max) Bounds(int[] values)
{
    int min = values[0];
    int max = values[0];
    foreach (int v in values)
    {
        if (v < min) min = v;
        if (v > max) max = v;
    }
    return (min, max);
}

Console.WriteLine(Square(7));
Console.WriteLine(Label("Хлеб"));
Console.WriteLine(Label("Сыр", suffix: "кг", count: 2));
Console.WriteLine(Sum());
Console.WriteLine(Sum(1, 2, 3));
var (lo, hi) = Bounds(new[] { 4, -2, 9 });
Console.WriteLine($"{lo} {hi}");

if (int.TryParse("42", out int parsed))
{
    Console.WriteLine(parsed + 1);
}
Console.WriteLine(int.TryParse("4x", out int bad));
Console.WriteLine(bad);
```
```output
49
Хлеб: 1 шт.
Сыр: 2 кг
0
6
-2 9
43
False
0
```

`=>` задаёт метод-выражение. Параметры со значением по умолчанию можно опускать, а при вызове называть по имени (`suffix: "кг"`) в любом порядке. `params` собирает произвольное число аргументов в массив. Кортеж `(int Min, int Max)` возвращает несколько значений; разложение `var (lo, hi) = ...` создаёт две переменные. Параметр `out` - способ вернуть ещё одно значение: метод обязан его присвоить. У `int.TryParse` при неудаче `out`-переменная получает `0`, а возвращается `false`.

**Попробуйте.** Сделайте метод `Bounds` безопасным для пустого массива: пусть он возвращает `(0, 0)`. Перепишите `Sum` без `params`, на `int[]`, и сравните вызовы.

## Пример 2. Массивы: ссылки, равенство, границы

```csharp run
static void Fill(int[] data, int value)
{
    for (int i = 0; i < data.Length; i++)
    {
        data[i] = value;
    }
}

static void Reassign(int[] data)
{
    data = new int[] { 99 };
}

int[] data = new int[3];
Console.WriteLine(string.Join(",", data));
Fill(data, 7);
Console.WriteLine(string.Join(",", data));
Reassign(data);
Console.WriteLine(string.Join(",", data));

int[] alias = data;
alias[0] = 1;
Console.WriteLine($"{data[0]} {data == alias} {data == new int[] { 1, 7, 7 }}");
Console.WriteLine(data.SequenceEqual(new[] { 1, 7, 7 }));
try
{
    Console.WriteLine(data[3]);
}
catch (IndexOutOfRangeException e)
{
    Console.WriteLine(e.Message);
}
```
```output
0,0,0
7,7,7
7,7,7
1 True False
True
Index was outside the bounds of the array.
```

`new int[3]` заполняет массив нулями (для ссылочных элементов - `null`). `Fill` меняет содержимое массива, и вызывающий это видит; `Reassign` присваивает параметру новый массив, а переменная `data` у вызывающего остаётся прежней. `==` для массивов сравнивает ссылки: `alias` указывает на тот же объект, а литерал с теми же числами - на другой. Для сравнения содержимого есть `SequenceEqual`. Выход за границы бросает `IndexOutOfRangeException` - молчаливого чтения мусора, как в C++, нет.

## Пример 3. Словарь и множество

```csharp run
var prices = new Dictionary<string, decimal>();
prices["хлеб"] = 45m;
prices["сыр"] = 320m;
prices["хлеб"] = 50m;
Console.WriteLine(prices.Count);
try
{
    Console.WriteLine(prices["молоко"]);
}
catch (KeyNotFoundException e)
{
    Console.WriteLine(e.Message);
}
if (!prices.TryGetValue("молоко", out decimal milk))
{
    Console.WriteLine($"нет, milk = {milk}");
}
Console.WriteLine(prices.GetValueOrDefault("молоко", -1m));
try
{
    prices.Add("хлеб", 1m);
}
catch (ArgumentException e)
{
    Console.WriteLine(e.GetType().Name);
}

var counts = new Dictionary<char, int>();
foreach (char c in "banana")
{
    counts[c] = counts.GetValueOrDefault(c) + 1;
}
Console.WriteLine(string.Join(" ", counts.Keys.Order().Select(k => $"{k}={counts[k]}")));

var seen = new HashSet<int>();
Console.WriteLine($"{seen.Add(5)} {seen.Add(5)} {seen.Count}");
```
```output
2
The given key 'молоко' was not present in the dictionary.
нет, milk = 0
-1
ArgumentException
a=3 b=1 n=2
True False 1
```

Запись `d[key] = value` добавляет или заменяет. Чтение `d[key]` для отсутствующего ключа бросает `KeyNotFoundException`, поэтому для необязательных ключей применяют `TryGetValue` или `GetValueOrDefault`. `Add` с существующим ключом бросает `ArgumentException`. Порядок обхода словаря гарантированным не считается, и если нужен стабильный порядок (в выводе, в тестах), ключи копируют в список или берут `Order()`/`Sort`. `HashSet<T>` хранит уникальные значения: `Add` возвращает `false`, если элемент уже есть - удобный способ убирать дубликаты.

## Пример 4. Список: изменение при обходе, копии и ссылки

```csharp run
var numbers = new List<int> { 1, 2, 3, 4 };
try
{
    foreach (int n in numbers)
    {
        if (n % 2 == 0) numbers.Remove(n);
    }
}
catch (InvalidOperationException e)
{
    Console.WriteLine(e.Message);
}
numbers.RemoveAll(n => n % 2 == 0);
Console.WriteLine(string.Join(",", numbers));

var first = new List<int> { 1, 2 };
var second = first;
second.Add(3);
var copy = new List<int>(first);
copy.Add(4);
Console.WriteLine($"{first.Count} {second.Count} {copy.Count} {first == second} {first == copy}");

Console.WriteLine(numbers[^1]);
Console.WriteLine(string.Join(",", numbers.GetRange(0, 1)));
try
{
    Console.WriteLine(numbers[5]);
}
catch (ArgumentOutOfRangeException e)
{
    Console.WriteLine(e.GetType().Name);
}
```
```output
Collection was modified; enumeration operation may not execute.
1,3
3 3 4 True False
3
1
ArgumentOutOfRangeException
```

Список нельзя менять во время `foreach`: следующий шаг обхода бросает `InvalidOperationException`. Удалять элементы по условию безопаснее через `RemoveAll`, а для другой логики обходят копию (`foreach (var x in new List<int>(list))`) или идут циклом `for` от конца. `var second = first` не копирует список, `new List<int>(first)` копирует **элементы** (поверхностно: вложенные ссылочные объекты остаются общими). Индексатор по списку с неверным индексом бросает `ArgumentOutOfRangeException`, а не `IndexOutOfRangeException`, как массив. `^1` - последний элемент.

<!-- section: precise -->
## Точные правила

* Параметры передаются по значению; для ссылочных типов копируется ссылка. `ref` и `out` передают саму переменную. Аргумент для `out` обязан быть присвоен внутри метода до `return`.
* Необязательные параметры идут в конце списка; именованные аргументы можно переставлять. `params` - последний параметр и массив.
* Массив имеет фиксированную длину (`Length`), элементы получают значения по умолчанию. Многомерные: `int[,]` прямоугольный, `int[][]` зубчатый.
* `List<T>`: `Add`, `Insert`, `Remove` (первое вхождение), `RemoveAt`, `RemoveAll`, `Contains`, `IndexOf`, `Sort`, `Count`. `Sort()` нестабилен; стабильная сортировка - LINQ `OrderBy`.
* `Dictionary<TKey, TValue>`: ключи уникальны, поиск за O(1). Для своих типов ключа нужны согласованные `Equals` и `GetHashCode`. Порядок обхода не гарантирован.
* `foreach` читает коллекцию через итератор и запрещает структурные изменения (бросает `InvalidOperationException`). Присваивать переменной цикла нельзя.
* Строки как ключи: по умолчанию сравнение порядковое (`Ordinal`); регистронезависимый словарь - `new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)`.
* Сортировка строк для воспроизводимого результата: `list.Sort(StringComparer.Ordinal)`.
* Диапазоны `a[1..3]` для массива создают копию, для `string` - подстроку; `^n` отсчитывает с конца.

<!-- section: mistakes -->
## Типичные ошибки

**1. Присваивание вместо копирования.** `var b = a;` даёт два имени одного списка. Копия - `new List<int>(a)` или `a.ToArray()`; глубокая копия вложенных объектов - вручную.

**2. `==` для массивов и списков.** Сравнивает ссылки. Используйте `SequenceEqual`.

**3. Изменение списка внутри `foreach`.** Ошибка появляется во время выполнения: `Collection was modified; enumeration operation may not execute.` Используйте `RemoveAll` или обход копии.

**4. `dict[key]` для необязательного ключа.** Бросает `KeyNotFoundException` с текстом `The given key 'молоко' was not present in the dictionary.` Проверьте `TryGetValue`.

**5. Обход словаря как упорядоченного.** Порядок не гарантирован, даже если сейчас совпадает с порядком вставки. Сортируйте ключи.

**6. Метод меняет вход и при этом возвращает результат.** Вызывающий получает неожиданный побочный эффект. Договоритесь: либо метод меняет коллекцию на месте (`void`), либо возвращает новую.

**7. Присваивание параметру-массиву.** `data = new int[] { 99 };` внутри метода не заменяет массив у вызывающего. Нужно изменить элементы либо вернуть новое значение.

**8. Остаток от деления на длину пустого массива.** Выражение `k % n` при `n == 0` бросает `DivideByZeroException`; проверяйте `n == 0` заранее.

**9. Сортировка строк без компаратора.** Порядок зависит от культуры машины. Передавайте `StringComparer.Ordinal`.

**10. Перепутанные исключения.** Массив бросает `IndexOutOfRangeException`, список - `ArgumentOutOfRangeException`; ловите нужное или проверяйте индекс.

<!-- section: summary -->
## Итог

Теперь вы знаете, как передаются параметры в C#, чем отличаются массив, список, словарь и множество, как безопасно читать ключи и как не менять коллекцию во время обхода. Следующий урок - ошибки, ввод и классы: [Ошибки, ввод и классы](lesson:csharp.programmers.errors-input-and-classes). Для подробностей: [Методы](lesson:csharp.from-zero.methods), [Списки List](lesson:csharp.from-zero.lists), [Словари Dictionary](lesson:csharp.from-zero.dictionaries).

<!-- section: programmer -->
## Главное для тех, кто уже программирует

* Параметры как в Java: значение, но для объектов - значение ссылки. В отличие от Java, есть `ref` и `out`, а в отличие от Python и JavaScript - необязательные и именованные аргументы на уровне языка.
* Несколько возвращаемых значений - кортежи (как в Go и Python); `out` нужен для паттерна `TryParse`.
* `List<T>` - аналог `ArrayList` в Java, `list` в Python, срез/`append` в Go. Массив фиксированной длины - отдельный тип, к списку не приводится.
* `Dictionary` - аналог `HashMap`; чтение отсутствующего ключа бросает исключение (в Python - `KeyError`, в Go - нулевое значение, в JavaScript - `undefined`). Порядок обхода не гарантирован (Python сохраняет порядок вставки, Java `HashMap` - нет).
* `==` для коллекций сравнивает ссылки, как в Java и JavaScript, в отличие от Python, где `==` сравнивает содержимое.
* `foreach` по изменяемой коллекции падает, как `ConcurrentModificationException` в Java.
* Обобщённые коллекции типобезопасны (`List<int>` хранит `int` без упаковки), в отличие от старых негенерированных `ArrayList`.
* LINQ (`Select`, `Where`, `OrderBy`) даёт функциональный стиль над коллекциями, как streams в Java; подробно - в курсе «C#: основы языка».
