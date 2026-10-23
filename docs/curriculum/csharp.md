# Трек C#: структура

Стандарт: [../curriculum-standards.md](../curriculum-standards.md). Эталоны: [go.md](go.md), [javascript.md](javascript.md). Маршрут: `content/routes/csharp.yaml`.

```
csharp-from-zero (уровень 0) ──┐
                               ├─> csharp-first-steps (IL, CLR, JIT; значимые и ссылочные типы) ─> csharp-fundamentals
csharp-for-programmers (1) ────┘     (ASP.NET Core и другие фреймворки опираются на fundamentals)
   зачитывает уроки csharp-from-zero
csharp-skill-check: проверки компетенций
```

## Особенности C#, которые определили порядок

* **Как запускается программа.** Приложение собирает один файл `Program.cs` как file-based app (`dotnet build Program.cs`,
  затем запуск собранной сборки). Класс `Program` и метод `Main` не нужны: файл состоит из **инструкций верхнего уровня**
  (top-level statements), которые выполняются сверху вниз. Поэтому в первом уроке нет обязательного шаблона, который
  пришлось бы объяснять заранее. Собственно устройство запуска (`Main`, сборка, IL, CLR, JIT, ReadyToRun, Native AOT) -
  в `csharp-first-steps` (урок 01) и `csharp-fundamentals`; в курсе с нуля оно только упоминается как «подробно позже».
* **Стандартные пространства имён подключены автоматически** (неявные `using`: `System`, `System.IO`, `System.Linq`,
  `System.Collections.Generic`, `System.Threading` и др.). Поэтому `Console` и `List<T>` работают без `using`. Единственная
  строка `using`, которая нужна в курсе, - `using System.Globalization;` (с урока 06) для `CultureInfo`. Она объясняется
  честно: «подключает пространство имён, где лежит CultureInfo».
* **Культура и десятичная точка.** `Console.WriteLine(3.5)` и `double.Parse("2.5")` зависят от региональных настроек
  системы: на русской системе вывод будет `3,5`, а разбор `"2.5"` бросит `FormatException`. Это не «баг», а правило
  форматирования, но проверка ожидает точку, поэтому урок 06 вводит строку
  `CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;` первой в программе, где печатаются или читаются дробные
  числа. Дальше она стоит во всех таких программах и заготовках, а `ToString("F2", CultureInfo.InvariantCulture)` (так
  записано в `csharp-first-steps`) показан как эквивалент для одного вызова. До урока 06 курс не печатает и не читает
  дробные числа.
* **Ввод вводится рано (урок 08)**, потому что `Console.ReadLine()` - один вызов, а программы с вводом проверяются
  разными входами. Это же первое место, где нужен `string?`: `ReadLine` возвращает `null`, когда ввод закончился. Чтобы код
  был без предупреждений nullable, используется `Console.ReadLine() ?? ""`; оператор `!` («доверяю компилятору») не
  преподаётся. Чтение «до конца ввода» - в уроке о `while` (13).
* **Явные типы и `var`.** Переменные сначала объявляются с явным типом (`int count = 3;`), `var` вводится в том же
  уроке как «тип выводит компилятор», с правилом: `var` допустим, когда тип виден справа.
* **Числа:** `int` для счёта, `double` для измерений, `decimal` для денег (урок 06 показывает `0.1 + 0.2` и почему деньги
  хранят в `decimal`). Переполнение `int`, `checked`, `long` - в `csharp-first-steps` (урок 01).
* **Методы.** Это `static`-методы, объявленные в том же файле после инструкций верхнего уровня. Слово `static` объясняется
  честно: «метод не использует переменные снаружи, всё нужное получает через параметры»; подробно про классы - урок 23.
  С урока 16 упражнения - это функции, которые вызывает **данный** код ввода/вывода: заготовка содержит готовую часть
  программы (чтение ввода и печать результата) и метод, который нужно написать; тесты меняют входные данные.
* **Коллекции:** сначала массив (`int[]`, фиксированная длина, индекс, `foreach`), потом `List<T>`, потом
  `Dictionary<TKey, TValue>`. Дженерики объясняются как «в угловых скобках - тип элементов»; своё обобщение - в
  `csharp-fundamentals`.
* **Исключения:** `try/catch` и `int.TryParse` в уроке 22; `finally`, `using`, свои типы исключений - `csharp-fundamentals`.
* **Классы - мост к `csharp-first-steps`:** урок 23 вводит `class` с полями и свойствами, `new`, список объектов и
  ссылочное поведение; `struct`, `record`, интерфейсы, наследование откладываются (first-steps/fundamentals).
* **Откладывается:** `Main` и namespaces, LINQ, `struct` и `record`, интерфейсы, лямбды и делегаты, `async`, `ref`/`out`
  (кроме использования `out` в `TryParse`: «результат кладётся в переменную»), `checked`, `Span`.
* **Предупреждения компилятора** считаются дефектом: примеры курса компилируются без warning (nullable включён).

## csharp-from-zero (уровень 0)

Id `csharp.from-zero.<slug>`; каталог `NN-<slug>`; файл решения - `Program.cs` (io). Все упражнения - `io`.

| # | slug | Тема | introduces | Проверка |
|---|---|---|---|---|
| **Блок 0. Знакомство** |||||
| 01 | first-program | что такое программа, исходный код, компилятор; C# и .NET; `Console.WriteLine`; инструкции верхнего уровня | csharp.program, csharp.console-writeline, csharp.string-literal, csharp.statement | io, A |
| 02 | compiler-errors | сообщения компилятора: `Program.cs(строка,столбец): error CSxxxx`; ошибка и предупреждение; сборка и запуск | csharp.compiler-error, csharp.build-and-run | io, B (исправить) |
| 03 | output | `Console.Write` и `WriteLine`, `\n`, `\t`, `\"`, `\\`, комментарии | csharp.console-write, csharp.escape-sequence, csharp.comment | io, B |
| **Блок 1. Значения** |||||
| 04 | variables-and-types | переменная, тип, `int`, `string`, `bool`, явный тип и `var`, присваивание | csharp.variable, csharp.type, csharp.int, csharp.string, csharp.bool, csharp.var, csharp.assignment | io, B |
| 05 | arithmetic | `+ - * / %`, целочисленное деление, приоритет, `+=`, `++` | csharp.arithmetic, csharp.integer-division, csharp.remainder, csharp.precedence, csharp.compound-assignment | io, B |
| 06 | double-and-decimal | `double`, `decimal`, неточность дробей, приведение `(double)`, культура и точка | csharp.double, csharp.decimal, csharp.cast, csharp.culture | io, B |
| 07 | strings | склейка, интерполяция `$"..."`, `Length`, форматы `:F2` | csharp.concatenation, csharp.interpolation, csharp.string-length, csharp.format-specifier | io, B |
| **Блок 2. Ввод** |||||
| 08 | reading-input | `Console.ReadLine`, `string?`, `?? ""` | csharp.read-line, csharp.null, csharp.null-coalescing | io, B |
| 09 | parsing-numbers | `int.Parse`, `double.Parse`, `decimal.Parse`, `FormatException` (предварительно) | csharp.parse | io, C |
| **Блок 3. Решения** |||||
| 10 | comparison-and-logic | `== != < > <= >=`, `bool`, `&& \|\| !`, сравнение строк | csharp.comparison, csharp.logical-operators | io, B |
| 11 | if-else | `if / else if / else`, блоки `{}` | csharp.if | io, C |
| 12 | switch | `switch`-инструкция и выражение `switch` | csharp.switch, csharp.switch-expression | io, C |
| **Блок 4. Циклы** |||||
| 13 | while-loop | `while`, счётчик, накопитель, чтение до конца ввода | csharp.while, csharp.end-of-input | io, C |
| 14 | for-loop | `for (int i = 0; ...; i++)` | csharp.for | io, C |
| 15 | loop-control | `break`, `continue`, вложенные циклы | csharp.break-continue, csharp.nested-loops | io, C |
| **Блок 5. Методы** |||||
| 16 | methods | `static`-метод, параметры, вызов, `void`, область видимости | csharp.method, csharp.parameter, csharp.scope | io (метод + данный ввод), B |
| 17 | return-values | `return`, типы результата, методы-условия (`bool`), `=>` | csharp.return, csharp.void-method | io (метод + данный ввод), C |
| **Блок 6. Коллекции** |||||
| 18 | arrays | `int[]`, `new int[n]`, литерал, индекс, `Length`, `foreach`, выход за границы | csharp.array, csharp.index, csharp.array-length, csharp.foreach | io, C |
| 19 | lists | `List<T>`: `Add`, `Count`, `Remove`, `Contains`, `Sort`, чтение n чисел | csharp.list, csharp.list-methods | io, C |
| 20 | dictionaries | `Dictionary<TKey, TValue>`: запись, чтение, `TryGetValue`, `ContainsKey`, обход, порядок | csharp.dictionary, csharp.try-get-value | io, C |
| 21 | string-methods | `Trim`, `Split`, `ToUpper`, `Substring`, `Contains`, `Replace`, `char`, неизменяемость | csharp.string-methods, csharp.char, csharp.split | io, C |
| **Блок 7. Надёжность и практика** |||||
| 22 | exceptions | исключение, `try/catch`, `int.TryParse`, `throw` | csharp.exception, csharp.try-catch, csharp.try-parse, csharp.throw | io, C |
| 23 | classes-basics | `class`, поля и свойства, `new`, список объектов, ссылка | csharp.class, csharp.property, csharp.object, csharp.reference-type | io, C |
| 24 | mini-project | мини-проект: бюджет из строк ввода (итоги по категориям, плохие строки) | - | io, D |

Мост: `csharp.first-steps.how-csharp-runs` зависит от `csharp.from-zero.classes-basics` (урок 23); последний урок курса -
мини-проект (24) - ведёт в first-steps через `next`.

## csharp-for-programmers (уровень 1)

Id `csharp.programmers.<slug>`; упражнение - io: данный `Main`-код читает ввод и вызывает методы ученика. Квиз из 5 вопросов
на ловушки C#: целочисленное деление и `int`-переполнение без `checked`, `double` и `decimal`, культура при форматировании
и разборе, `string` неизменяема и сравнивается по значению, `Dictionary` - порядок не гарантируется и `KeyNotFoundException`,
`foreach` не позволяет изменять коллекцию, `null` и nullable-аннотации, `int.Parse` против `TryParse`.

| # | slug | assessmentFor (`csharp.from-zero.*`) |
|---|---|---|
| 01 | syntax-and-control-flow | 01-07, 10-15 |
| 02 | methods-and-collections | 16-21 |
| 03 | errors-input-and-classes | 08, 09, 22, 23 |

## csharp-skill-check

Id `csharp.check.<slug>`; урок-проверка = упражнение (io) + квиз на чтение кода (порог 0.75).

| Компетенция | slug | assessmentFor |
|---|---|---|
| Основы: значения, условия, циклы, методы | basics | from-zero 01-07, 10-17 |
| Массивы, списки, словари, строки | collections | from-zero 18-21 |
| Ввод и ошибки | errors-input | from-zero 08, 09, 22 |
| Типы значений и ссылок, nullable | value-types | csharp.first-steps.value-types-and-nullable, csharp.fundamentals.type-system-and-memory |
| Классы, интерфейсы, наследование | classes | from-zero 23, csharp.fundamentals.classes-records-structs, csharp.fundamentals.interfaces-inheritance-polymorphism |
| Обобщения, коллекции и LINQ | collections-linq | csharp.fundamentals.generics-and-collections, csharp.fundamentals.linq |
| Исключения и ресурсы | exceptions | csharp.fundamentals.exceptions-and-resources |
| Делегаты, события и async/await | delegates-async | csharp.fundamentals.delegates-lambdas-events, csharp.fundamentals.async-await |

## Связи

* `csharp-first-steps`: `requires: [csharp-from-zero]`; урок 01 зависит от `csharp.from-zero.classes-basics`.
* `csharp-fundamentals`: `requires: [csharp-first-steps]` (уже задано).
