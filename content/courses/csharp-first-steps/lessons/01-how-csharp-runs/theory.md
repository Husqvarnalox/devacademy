<!-- section: simple -->
## C# компилируется дважды: сначала в IL, потом в машинный код

Программа на C# не превращается в машинный код сразу и не читается построчно. Путь состоит из двух этапов:

1. **Компилятор (Roslyn)** переводит исходный текст в **IL** (Intermediate Language, промежуточный язык) и упаковывает его в **сборку** (assembly) - файл `.dll` с IL-кодом и метаданными о типах.
2. **Среда выполнения CLR** (Common Language Runtime, часть .NET) загружает сборку и **JIT-компилятор** (just-in-time) переводит IL в машинный код процессора уже во время работы - метод за методом, при первом вызове.

В этом приложении цель одна: `dotnet build Program.cs` создаёт сборку, а затем `dotnet` запускает её. Ошибка типов находится на первом этапе, до запуска.

Программа читает **стандартный ввод** через `Console.In` (или `Console.ReadLine()`) и пишет в **стандартный вывод** через `Console.WriteLine`:

```csharp
string text = Console.In.ReadToEnd();
Console.WriteLine("Получено символов: " + text.Length);
```

Современный C# позволяет не писать класс `Program` и метод `Main`: инструкции верхнего уровня (top-level statements) выполняются как тело неявного `Main`.

<!-- section: precise -->
## Что делает каждый этап и что из этого следует

**Компиляция в IL.** Roslyn проверяет синтаксис и типы. Результат не привязан к процессору: одна и та же сборка работает на x64 и ARM, если установлен подходящий .NET.
Метаданные в сборке позволяют среде выполнения и отражению (`System.Reflection`) узнавать о типах, методах и полях.

**JIT и многоуровневая компиляция.** При первом вызове метода среда выполнения быстро компилирует его в код с минимальной оптимизацией (tier 0), чтобы программа стартовала быстро.
Если метод вызывается часто, его перекомпилируют с полной оптимизацией (tier 1). Поэтому первое выполнение кода медленнее последующих, а результат программы от уровня компиляции не зависит.
Существуют и другие режимы: **ReadyToRun** предварительно компилирует IL в машинный код при публикации, **Native AOT** выпускает самодостаточный машинный код без JIT (с ограничениями на отражение и динамическую генерацию кода).

**Управляемое выполнение.** CLR управляет памятью: объекты размещаются в управляемой куче, а освобождает их **сборщик мусора** (GC). Вы не вызываете `free`. Проверка границ массива и приведений типов встроена в среду выполнения.

**Типы чисел.** `int` - 32-битное, `long` - 64-битное знаковое целое, `double` - 64-битное число с плавающей точкой. **Переполнение целых по умолчанию не проверяется**:
`int.MaxValue + 1` во время выполнения даёт `int.MinValue` (число «заворачивается»). Блок `checked { ... }` или выражение `checked(x + 1)` включает проверку и бросает `OverflowException`.
Выражения из констант, которые заведомо переполняются, компилятор отвергает сам. Для сумм, которые могут выйти за 2 147 483 647, используйте `long`.

**Разбор чисел и культура.** `int.Parse("42")` бросает `FormatException` на мусоре; `int.TryParse(s, out var v)` возвращает `bool` и не бросает.
Форматирование и разбор по умолчанию зависят от **текущей культуры**: в одних культурах десятичный разделитель точка, в других запятая. Для данных, которые читает программа или проверяющая система, указывают `CultureInfo.InvariantCulture`:
`x.ToString("F2", CultureInfo.InvariantCulture)` всегда даёт `2.50`.

**Деление.** `7 / 2` для `int` равно `3` (целочисленное деление, результат усекается к нулю). Чтобы получить `3.5`, хотя бы один операнд делают `double`: `(double)sum / count`.

**Разбор текста.** `text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)` делит по любым пробельным символам и убирает пустые элементы. LINQ (`System.Linq`) даёт `Sum()`, `Min()`, `Max()`, `Average()` для последовательностей чисел.

**Ввод.** `Console.ReadLine()` возвращает `string?`: `null`, когда ввод закончился. `Console.In.ReadToEnd()` читает всё сразу и возвращает пустую строку для пустого ввода.

<!-- section: examples -->
## Примеры

**Пример 1. В сборке лежит IL.** Отражение показывает байты IL-кода метода.

```csharp run
using System;

byte[] il = typeof(Demo).GetMethod("Add")!.GetMethodBody()!.GetILAsByteArray()!;
Console.WriteLine(il.Length > 0);
Console.WriteLine(typeof(int).Assembly.GetName().Name);
Console.WriteLine(Demo.Add(2, 3));

static class Demo
{
    public static int Add(int a, int b) => a + b;
}
```
```output
True
System.Private.CoreLib
5
```

Метод `Add` хранится в сборке как последовательность байтов IL, которую CLR превратит в машинный код при первом вызове. Базовый тип `int` определён в библиотеке `System.Private.CoreLib`, загруженной средой выполнения.

**Пример 2. Переполнение не замечается, пока вы не попросите.**

```csharp run
using System;

int x = int.MaxValue;
x++;
Console.WriteLine(x);

int y = int.MaxValue;
try
{
    y = checked(y + 1);
}
catch (OverflowException)
{
    Console.WriteLine("OverflowException");
}

long big = 2_000_000_000L + 2_000_000_000L;
Console.WriteLine(big);
```
```output
-2147483648
OverflowException
4000000000
```

Для `int` сумма не помещается и «заворачивается», а `checked` превращает это в исключение. Тип `long` вмещает результат.

**Пример 3. Деление и форматирование.**

```csharp run
using System;
using System.Globalization;

Console.WriteLine(7 / 2);
Console.WriteLine((7 / 2.0).ToString(CultureInfo.InvariantCulture));
Console.WriteLine((5.0 / 3).ToString("F2", CultureInfo.InvariantCulture));
Console.WriteLine(int.TryParse("12x", out int v) ? "число" : "не число");
```
```output
3
3.5
1.67
не число
```

`TryParse` не бросает исключение: он возвращает `false`, а `v` в этом случае равен `0`. Без `InvariantCulture` строка `3.5` на компьютере с другой региональной настройкой могла бы напечататься как `3,5`: так делает `Console.WriteLine(7 / 2.0)` при русской или немецкой культуре.

**Пример 4. Диагностика не попадает в stdout.**

```csharp run
using System;

Console.WriteLine("результат");
Console.Error.WriteLine("диагностика");
```
```output
результат
```

**Пример 5. Типовая форма программы.**

```csharp
using System;
using System.Globalization;
using System.Linq;

string text = Console.In.ReadToEnd();
string[] tokens = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
int n = int.Parse(tokens[0], CultureInfo.InvariantCulture);
long[] nums = tokens.Skip(1).Take(n).Select(t => long.Parse(t, CultureInfo.InvariantCulture)).ToArray();
Console.WriteLine(nums.Sum());
```

<!-- section: mistakes -->
## Типичные ошибки

* **Суммировать в `int`.** Сумма двух чисел до 2 000 000 000 уже не помещается. Тихое переполнение даёт отрицательное число без ошибки. Берите `long`.
* **Целочисленное деление вместо дробного.** `sum / count` для двух `int` отбрасывает дробную часть. Приведите одно из чисел к `double`.
* **Форматирование без `CultureInfo.InvariantCulture`.** На компьютере с другой культурой вывод получит запятую вместо точки.
* **`int.Parse` на пустом токене.** Пустая строка даёт `FormatException`. Убирайте пустые элементы через `StringSplitOptions.RemoveEmptyEntries`.
* **Считать, что `Console.ReadLine()` всегда возвращает строку.** В конце ввода он возвращает `null`.
* **Ждать, что `Average()` вернёт `int`.** Для целых последовательностей он возвращает `double`.
* **Путать сборку и исполняемый файл.** `.dll` содержит IL и запускается через `dotnet`; сам по себе процессор его не выполняет.
* **Класть логику после объявления типов в файле с top-level statements.** Инструкции верхнего уровня должны стоять до объявлений типов.

<!-- section: summary -->
## Итоги

* C# компилируется в IL (сборка `.dll`), а CLR компилирует IL в машинный код JIT-компилятором при выполнении; бывают ReadyToRun и Native AOT.
* Многоуровневая JIT быстро запускает код и оптимизирует горячие методы; результат от этого не меняется.
* Переполнение целых по умолчанию не проверяется: используйте `long` или `checked`.
* Ввод: `Console.In.ReadToEnd()`, `Console.ReadLine()`; вывод: `Console.WriteLine`; для чисел с точкой задавайте `CultureInfo.InvariantCulture`.

Дальше: [значимые и ссылочные типы, nullable](lesson:csharp.first-steps.value-types-and-nullable).
Для сравнения: [запуск программы на Python](lesson:python.fundamentals.how-python-runs).
