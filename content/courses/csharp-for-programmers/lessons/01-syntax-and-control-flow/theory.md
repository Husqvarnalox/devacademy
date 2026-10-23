<!-- section: simple -->
## Зачем этот урок

Синтаксис C# знаком каждому, кто писал на Java, C++ или Go: фигурные скобки, статические типы, `if`, `for`, `while`. Ловушки лежат в числах и строках: `int` молча переполняется, деление целых отбрасывает дробную часть, остаток от деления отрицательного числа отрицателен, `Math.Round` по умолчанию округляет «до чётного», а форматирование и разбор чисел зависят от региональных настроек машины. Успешное прохождение (упражнение и квиз) засчитывает уроки 01-07 и 10-15 курса «C# с нуля».

## Главная мысль

C# строгий, но не везде: компилятор не даст присвоить `double` в `int` без приведения, зато арифметика целых по умолчанию **не проверяет** переполнение, а числа и строки по умолчанию форматируются **по культуре компьютера**. Всё, что важно для воспроизводимого результата (границы значений, режим округления, культуру), нужно указывать явно.

<!-- section: examples -->
## Пример 1. Целые числа: переполнение, деление, остаток

Файл программы состоит из инструкций верхнего уровня - `Main` и класс писать не нужно. Простые пространства имён (`System`, `System.Collections.Generic`, `System.Linq` и другие) подключены автоматически, поэтому `Console` и `List<T>` доступны без `using`. Первые строки примера задают культуру: без них на русской системе `3.5` выводилось бы как `3,5`.

```csharp run
using System.Globalization;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

int big = int.MaxValue;
Console.WriteLine(big + 1);
Console.WriteLine((long)big + 1);
try
{
    Console.WriteLine(checked(big + 1));
}
catch (OverflowException e)
{
    Console.WriteLine(e.GetType().Name);
}

Console.WriteLine(7 / 2);
Console.WriteLine(-7 / 2);
Console.WriteLine(-7 % 3);
Console.WriteLine(7 / 2.0);
double ratio = 10 / 4;
Console.WriteLine(ratio);
Console.WriteLine((int)3.99);
Console.WriteLine((int)-3.99);

int zero = 0;
try
{
    Console.WriteLine(5 / zero);
}
catch (DivideByZeroException e)
{
    Console.WriteLine(e.GetType().Name);
}
Console.WriteLine(5.0 / zero);
Console.WriteLine(0.0 / zero);
```
```output
-2147483648
2147483648
OverflowException
3
-3
-1
3.5
2
3
-3
DivideByZeroException
Infinity
NaN
```

`int` - 32 бита, и `big + 1` переходит через границу и становится `-2147483648`, без ошибки и предупреждения. Оператор `checked(...)` включает проверку, и тогда бросается `OverflowException`; для безопасного вычисления берут более широкий тип (`long`). Деление двух `int` целочисленное и усекает к нулю: `-7 / 2` равно `-3`, а не `-4`, поэтому остаток `-7 % 3` получает знак делимого и равен `-1`. В `double ratio = 10 / 4` сначала выполняется целочисленное деление (`2`), и только потом результат превращается в `double`. Приведение `(int)` отбрасывает дробную часть. Целое деление на ноль бросает исключение, а деление `double` на ноль даёт `Infinity` или `NaN`.

**Попробуйте.** Добавьте `unchecked`-вариант `(long)big * 2` и сравните его с `big * 2`. Посчитайте среднее двух больших `int` формулой `(a + b) / 2` и формулой `a + (b - a) / 2`.

## Пример 2. double, decimal и округление

```csharp run
using System.Globalization;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

Console.WriteLine(0.1 + 0.2);
Console.WriteLine(0.1 + 0.2 == 0.3);
Console.WriteLine(0.1m + 0.2m);
Console.WriteLine(0.1m + 0.2m == 0.3m);

Console.WriteLine(Math.Round(2.5));
Console.WriteLine(Math.Round(3.5));
Console.WriteLine(Math.Round(2.5, MidpointRounding.AwayFromZero));
Console.WriteLine(Math.Round(2.665m, 2));
Console.WriteLine(Math.Round(2.665m, 2, MidpointRounding.AwayFromZero));

double p = 0.1;
decimal q = 0.1m;
Console.WriteLine($"{p * 3} {q * 3}");
Console.WriteLine(1234.5678m.ToString("F2"));
Console.WriteLine(double.Parse("2.5") * 2);

double x = 0.0 / 0.0;
double y = 0.0 / 0.0;
Console.WriteLine($"{x == y} {x.Equals(y)} {double.IsNaN(x)}");
```
```output
0.30000000000000004
False
0.3
True
2
4
3
2.66
2.67
0.30000000000000004 0.3
1234.57
5
False True True
```

`double` хранит двоичную дробь, поэтому `0.1 + 0.2` не равно `0.3`. `decimal` хранит десятичную дробь и подходит для денег; суффикс `m` обязателен у литерала (`0.1m`), а смешивать `decimal` и `double` в одном выражении компилятор не позволит. `Math.Round` по умолчанию округляет половину **к ближайшему чётному**: `2.5` даёт `2`, `3.5` даёт `4`, `2.665m` даёт `2.66`. Привычное «половина вверх» включается явно: `MidpointRounding.AwayFromZero`. `NaN` не равен самому себе при `==`, но `Equals` и `double.IsNaN` его распознают.

**Культура.** `ToString`, интерполяция `$"{x}"`, `double.Parse` и `decimal.Parse` используют `CultureInfo.CurrentCulture`. На машине с русскими настройками `1.5.ToString()` даёт `1,5`, а `double.Parse("2.5")` бросает `FormatException`. Для воспроизводимого результата первой строкой программы выставляют `CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;` либо передают культуру в конкретный вызов: `x.ToString("F2", CultureInfo.InvariantCulture)`.

## Пример 3. Строки и символы

```csharp run
string a = "caf" + "e";
string b = new string(new[] { 'c', 'a', 'f', 'e' });
Console.WriteLine(a == b);
Console.WriteLine(ReferenceEquals(a, b));
Console.WriteLine("a" == "A");
Console.WriteLine(string.Equals("a", "A", StringComparison.OrdinalIgnoreCase));

string s = "hello";
string t = s.ToUpper();
Console.WriteLine($"{s} {t}");

Console.WriteLine("😀".Length);
Console.WriteLine('a' + 1);
Console.WriteLine((char)('a' + 1));
Console.WriteLine("abcdef"[1..4]);
Console.WriteLine("abcdef"[^2]);

string? maybe = null;
Console.WriteLine(maybe?.Length ?? -1);
Console.WriteLine("x" + null + 1 + 2);
Console.WriteLine(1 + 2 + "x");

var names = new List<string> { "b", "a", "C", "B" };
names.Sort(StringComparer.Ordinal);
Console.WriteLine(string.Join(",", names));
```
```output
True
False
False
True
hello HELLO
2
98
b
bcd
e
-1
x12
3x
B,C,a,b
```

В отличие от Java, `==` для `string` сравнивает **содержимое** (оператор переопределён); сравнение по ссылке - `ReferenceEquals`. Строки неизменяемы: `ToUpper` возвращает новую строку, а `s` остаётся прежней. `Length` считает **кодовые единицы UTF-16**, а не символы: эмодзи занимает две единицы. Арифметика с `char` даёт `int` (`'a' + 1` равно `98`), поэтому обратно нужно приведение `(char)`. Диапазон `s[1..4]` берёт элементы с индекса 1 до 3 включительно, `^2` - второй с конца. Склейка слева направо: `"x" + null + 1 + 2` даёт `x12`, а `1 + 2 + "x"` сначала складывает числа. Сортировка строк без указания сравнения учитывает правила языка системы и может давать разный порядок на разных машинах; `StringComparer.Ordinal` сравнивает по кодам символов (заглавные буквы идут перед строчными) и воспроизводим везде.

## Пример 4. switch-выражение, паттерны и кортежи

```csharp run
static string Grade(int score) => score switch
{
    >= 90 => "A",
    >= 75 => "B",
    >= 60 => "C",
    _ => "D"
};

static string Describe(object? value) => value switch
{
    int n when n < 0 => "отрицательное целое",
    int => "целое",
    string { Length: 0 } => "пустая строка",
    string s => $"строка длиной {s.Length}",
    null => "null",
    _ => "что-то другое"
};

foreach (int score in new[] { 95, 75, 59 })
{
    Console.WriteLine($"{score}: {Grade(score)}");
}
foreach (object? v in new object?[] { -3, 7, "", "abc", null, 2.5 })
{
    Console.WriteLine(Describe(v));
}

var items = new List<int> { 3, 1, 2 };
var pair = (Min: items.Min(), Max: items.Max());
Console.WriteLine($"{pair.Min}..{pair.Max}");
(int a, int b) = (1, 2);
(a, b) = (b, a);
Console.WriteLine($"{a} {b}");
```
```output
95: A
75: B
59: D
отрицательное целое
целое
пустая строка
строка длиной 3
null
что-то другое
1..3
2 1
```

Выражение `switch` возвращает значение и сопоставляет с **образцами**: сравнение `>= 90`, тип `int`, свойство `{ Length: 0 }`, условие `when`, `null`. Ветки проверяются сверху вниз, а образец `_` ловит остальное; если ни одна ветка не подошла, бросается исключение, поэтому `_` почти всегда нужен. Классическая инструкция `switch` не «проваливается» между метками: каждая ветка заканчивается `break` или `return`, иначе ошибка компиляции. `var` выводит тип из правой части, а кортеж `(Min: ..., Max: ...)` - лёгкий способ вернуть два значения.

<!-- section: precise -->
## Точные правила

* Целочисленные операции (`+ - *`) по умолчанию **не проверяют** переполнение (контекст `unchecked`); `checked(...)` и блок `checked { }` включают проверку. Константное выражение, которое переполняется (`int.MaxValue + 1`), отвергается компилятором.
* Деление `int / int` усекает к нулю; остаток `%` имеет знак делимого: `a == (a / b) * b + a % b`. Для неотрицательного остатка: `((a % b) + b) % b`.
* Неявные приведения идут только «вширь» (`int` в `long`, `long` в `double`); сужение требует `(int)x`. `(int)` от `double` усекает к нулю; значение вне диапазона даёт неопределённый результат.
* `double` - двоичная дробь IEEE 754, `decimal` - 28-29 значащих десятичных цифр, подходит для денег. `float` и `double` не сравнивают через `==`, `NaN != NaN`.
* `Math.Round(x)` - к ближайшему чётному (банковское округление), `MidpointRounding.AwayFromZero` - половина от нуля. `Math.Floor` и `Math.Ceiling` округляют вниз и вверх.
* `string` - ссылочный неизменяемый тип с оператором `==` по значению. `==` для `string` чувствителен к регистру; `Equals(a, b, StringComparison.Ordinal / OrdinalIgnoreCase)` задаёт правила явно.
* Культура: `CultureInfo.CurrentCulture` влияет на `ToString`, интерполяцию, `Parse`, `Sort` и сравнение строк через `CompareTo`; `InvariantCulture` стабилен.
* `switch` - инструкция (без «проваливания») и выражение (возвращает значение, образцы). `foreach` не даёт менять переменную цикла.
* Включённые nullable-аннотации (`string?`) - предупреждения компилятора, а не проверка во время выполнения.

<!-- section: mistakes -->
## Типичные ошибки

**1. Неявное сужение.** `int n = 3.7;` не компилируется:

```
Program.cs(1,9): error CS0266: Cannot implicitly convert type 'double' to 'int'. An explicit conversion exists (are you missing a cast?)
```

Нужно явное приведение `(int)3.7` либо `Math.Round`/`Math.Floor` с приведением к `int`.

**2. Присваивание в условии.** `if (n = 3)` - ошибка, потому что `int` не `bool`:

```
Program.cs(14,5): error CS0029: Cannot implicitly convert type 'int' to 'bool'
```

Для переменной типа `bool` запись `if (flag = true)` компилируется (с предупреждением), поэтому сравнивайте `flag == true` или просто пишите `if (flag)`.

**3. Провал между метками `switch`.**

```
Program.cs(8,5): error CS0163: Control cannot fall through from one case label ('case "a":') to another
```

Завершите ветку `break`, `return` или `continue`.

**4. Неинициализированная локальная переменная.** `int x; Console.WriteLine(x);` даёт `error CS0165: Use of unassigned local variable 'x'`. Значения по умолчанию получают только поля и элементы массивов.

**5. Константное переполнение.** `const int k = int.MaxValue + 1;` - `error CS0220: The operation overflows at compile time in checked mode`. А такое же вычисление над переменной не даёт ни ошибки, ни предупреждения.

**6. `(a + b) / 2` для больших `int`.** Сумма переполняется до деления и результат становится отрицательным. Считайте в `long` или пишите `a + (b - a) / 2`.

**7. Забыли про культуру.** Программа, которая печатает `3.5`, на русской системе печатает `3,5`, а `double.Parse("3.5")` падает. Укажите `InvariantCulture`.

**8. Деньги в `double`.** Накопленная погрешность видна на сотнях операций. Используйте `decimal` и явный режим округления.

**9. `Length` как «число символов».** Для текстов с эмодзи и составными знаками оно отличается от числа видимых символов.

**10. Сортировка строк без компаратора.** Порядок зависит от культуры; в тестах и выводе, который должен быть одинаков на всех машинах, используйте `StringComparer.Ordinal`.

<!-- section: summary -->
## Итог

Вы знаете главные отличия C# в числах и строках: `int` молча переполняется, деление и остаток работают «к нулю», `Math.Round` округляет до чётного, `decimal` нужен для денег, а форматирование и разбор зависят от культуры. Следующий урок - методы, массивы, списки и словари: [Методы и коллекции](lesson:csharp.programmers.methods-and-collections). Для подробностей: [Дробные числа: double и decimal](lesson:csharp.from-zero.double-and-decimal), [Арифметика с целыми числами](lesson:csharp.from-zero.arithmetic).

<!-- section: programmer -->
## Главное для тех, кто уже программирует

* Синтаксис ближе всего к Java, но с отличиями: `==` для строк сравнивает содержимое, нет проверяемых исключений, есть значимые типы (`struct`, `int`) и беззнаковые целые (`uint`, `ulong`, `byte`).
* Целочисленное переполнение молчит, как в Java и Go и в отличие от Python (там `int` неограничен) и Rust в debug-сборке (паника). `checked` включает проверку только для заданного участка.
* Целое деление к нулю и знак остатка - как в Java, C++ и Go; в Python `//` и `%` округляют вниз, поэтому `-7 // 2 == -4`.
* `Math.Round` - банковское округление, как `round()` в Python 3; в Java `Math.round` округляет половину вверх.
* `decimal` - готовый десятичный тип для денег (в Java это `BigDecimal`, в Go такого типа в стандартной библиотеке нет). Литерал требует `m`.
* Строки неизменяемы, как в Java и Python; кодировка UTF-16 (`char` - 16 бит), в Go строка - байты UTF-8.
* Инструкции верхнего уровня (`Console.WriteLine("...")` в файле без `Main`) - как скрипт в Python; под капотом компилятор создаёт `Main`.
* Паттерны в `switch` ближе к `match` в Rust и Python, чем к `switch` в Java 8; выражение `switch` должно быть исчерпывающим.
* Региональные настройки влияют на ввод-вывод по умолчанию - в JavaScript, Go и Java `toString` для чисел от них не зависит.
