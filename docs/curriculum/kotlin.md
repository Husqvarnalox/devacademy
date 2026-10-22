# Трек Kotlin: структура

Стандарт: [../curriculum-standards.md](../curriculum-standards.md). Эталоны: [go.md](go.md), [javascript.md](javascript.md), ближайшие по устройству - [java.md](java.md) и [csharp.md](csharp.md). Маршрут: `content/routes/kotlin.yaml`.

```
kotlin-from-zero (уровень 0) ──┐
                               ├─> kotlin-first-steps (kotlinc, JVM, Long и переполнение; null-безопасность) ─> kotlin-fundamentals
kotlin-for-programmers (1) ────┘
   зачитывает уроки kotlin-from-zero
kotlin-skill-check: проверки компетенций
```

## Особенности Kotlin, которые определили порядок

* **Как запускается программа.** Каждое упражнение - один файл `Main.kt` (`entryFile`). Приложение собирает все `.kt`-файлы
  каталога командой `kotlinc ... -include-runtime -d prog.jar`, затем запускает `java -jar prog.jar` (нужны `kotlinc` и JDK;
  примеры проверены на Kotlin 2.4). Сборка занимает несколько секунд, потому что сам `kotlinc` - программа на JVM. В
  уроке 01 это объясняется простыми словами (два шага: «перевести» и «выполнить»); байткод, jar и стандартная библиотека в
  архиве - в разделе «Углубление» и в `kotlin-first-steps` (урок 01).
* **Обёртки нет.** Достаточно `fun main() { ... }` на верхнем уровне файла: ни класса, ни `args`, ни `static`. Поэтому
  в уроке 01 нет «слов, которые пока нужно принять на веру», кроме `fun` («объявление функции»), и оно честно связано с
  уроком 17. Слово `class` появляется только в уроке 23.
* **Ввод вводится рано (урок 08).** `readln()` - один вызов без импорта и без объекта-читателя, поэтому упражнения могут
  проверяться на разных данных. До урока 08 stdin пуст, и проверяется фиксированный вывод. `readln()` читает одну строку;
  числа в строках читаются через `toInt()` (урок 09). На пустом вводе `readln()` бросает `ReadAfterEOFException`;
  чтение «пока есть данные» через `readlnOrNull()` - урок 18, поэтому уроки о циклах 14-16 читают заданное число значений
  или значение-ограничитель.
* **`val` по умолчанию, `var` когда значение меняется** (урок 04). `val` объясняется как «имя, которое нельзя
  переназначить»; изменяемость содержимого коллекций (`val` не делает список неизменяемым) - урок 19.
* **Шаблоны строк `"$name"` и `"${a + b}"`** идут вместе с переменными (урок 04), потому что печатать значения
  переменных приходится сразу; склейка `+` показана в уроке 03 и объяснена как менее удобная.
* **Числа без неявных преобразований.** `Int`, `Double`, `Boolean`, `Char`, `String` - уроки 04-05; `Long` вводится в
  уроке 05 одним абзацем («целое с большим диапазоном»; литерал `3000000000` Kotlin сам делает `Long`); переполнение `Int`
  и `Long` подробно - `kotlin-first-steps`. Явные `toInt()`, `toDouble()` - урок 09; при делении целых получается целое
  (урок 06), и это разбирается как типичная ловушка.
* **Форматирование дробных чисел зависит от локали компьютера.** На русской системе `"%.2f".format(2.5)` даёт `2,50`
  (проверено: даже без параметров запуска). `println(2.5)` и `"2.5".toDouble()` от локали **не зависят**. Поэтому в
  уроке 10 форматирование записывается как `"%.2f".format(Locale.ROOT, x)` с `import java.util.Locale`; причина объясняется
  один раз, дальше строка просто повторяется. `import` объясняется как «где искать `Locale`».
* **`==` сравнивает содержимое** строк и чисел одного типа (ловушки Java нет); `5 == 5.0` - ошибка компиляции, это
  разбирается в уроке 11. `===` (сравнение ссылок) не преподаётся.
* **`if` и `when` - выражения** (уроки 12-13): `val m = if (a > b) a else b`. `when` вместо `switch`, без `break`.
* **Диапазоны:** `1..n`, `until`, `downTo`, `step` - вместе с `for (i in ...)` (урок 15); `in` внутри `when` - урок 13.
* **Null-безопасность - ровно столько, сколько нужно.** Урок 18 вводит `Int?`/`String?` как «число или ничего»,
  `toIntOrNull()`, `readlnOrNull()`, `?:` и проверку `== null`. Безопасный вызов `?.`, `!!`, smart cast, платформенные типы,
  `lateinit` - `kotlin-first-steps` (урок 02) и `kotlin-fundamentals`.
* **Коллекции без лямбд** (уроки 19-20): `listOf`/`mutableListOf`, `mapOf`/`mutableMapOf`, `for`, `sum()`, `sorted()`,
  `joinToString()`. `filter { }`, `map { }`, `Sequence`, функции высшего порядка - `kotlin-fundamentals` (урок 04).
* **Исключения** (урок 22): `try/catch`, `throw`, `NumberFormatException`; `Result`, `require`/`check`, иерархия - в
  `kotlin-fundamentals` (`error-handling`).
* **Первый класс** (урок 23) - мост: обычный `class` с свойствами в конструкторе и `data class`; `object`, `sealed`,
  наследование, интерфейсы - `kotlin-fundamentals` (`classes-sealed-and-when`).
* **Что отложено:** аргументы по умолчанию и именованные аргументы, функции-расширения, лямбды, scope-функции, обобщения,
  корутины, взаимодействие с Java. Во from-zero они встречаются только в разделах «Для тех, кто уже программирует» и
  «Углубление».

## kotlin-from-zero (уровень 0)

Id `kotlin.from-zero.<slug>`; каталог `NN-<slug>`; файл решения - `Main.kt`. Все упражнения - `io`.

| # | slug | Тема | introduces | Проверка |
|---|---|---|---|---|
| **Блок 0. Знакомство** |||||
| 01 | first-program | что такое программа и компилятор; `fun main()`; `println`, строковый литерал | kotlin.program, kotlin.compiler, kotlin.main-function, kotlin.println, kotlin.string-literal | io, A |
| 02 | compiler-errors | как читать сообщение `kotlinc`: файл, строка, столбец, `^`, каскад ошибок | kotlin.compile-error, kotlin.error-location | io, B (исправить) |
| 03 | output | `print` и `println`, `\n`, `\"`, склейка `+`, комментарии | kotlin.print, kotlin.escape-sequence, kotlin.concatenation, kotlin.comment | io, B |
| **Блок 1. Значения** |||||
| 04 | variables | `val`, `var`, присваивание, вывод типа, `Int`, `String`, шаблоны строк | kotlin.variable, kotlin.val, kotlin.var, kotlin.assignment, kotlin.type-inference, kotlin.int, kotlin.string, kotlin.string-template | io, B |
| 05 | basic-types | `Double`, `Boolean`, `Char`, `Long`, явный тип `: Тип` | kotlin.type, kotlin.double, kotlin.boolean, kotlin.char, kotlin.long, kotlin.explicit-type | io, B |
| 06 | arithmetic | `+ - * / %`, целочисленное деление, приоритет, `+=`, `++` | kotlin.arithmetic, kotlin.integer-division, kotlin.remainder, kotlin.compound-assignment | io, B |
| 07 | strings | `length`, индекс `s[i]`, `uppercase()`, `lowercase()`, индексы с нуля | kotlin.string-length, kotlin.string-index, kotlin.string-case | io, B |
| 08 | reading-input | `readln()`, stdin, строка как единица ввода, ввод по строкам | kotlin.readln, kotlin.stdin | io, C |
| 09 | type-conversion | `toInt()`, `toDouble()`, `toString()`, `NumberFormatException`, деление после `toDouble()` | kotlin.to-int, kotlin.to-double, kotlin.to-string, kotlin.number-format-exception | io, B |
| 10 | formatted-output | `"%.2f".format(Locale.ROOT, x)`, `%d %s`, `import`, `roundToInt()` | kotlin.format, kotlin.format-specifier, kotlin.locale, kotlin.import, kotlin.round | io, B |
| 11 | comparison-and-logic | `== != < > <= >=`, `&& \|\| !`, приоритет, короткое вычисление | kotlin.comparison, kotlin.logical-operators, kotlin.boolean-expression, kotlin.short-circuit | io, B |
| **Блок 2. Управление** |||||
| 12 | if-else | `if / else if / else`, блок `{}`, `if` как выражение | kotlin.if, kotlin.else, kotlin.block, kotlin.if-expression | io, C |
| 13 | when | `when` со значением и без, `else`, несколько значений, `in 1..9`, `when` как выражение | kotlin.when, kotlin.when-branch, kotlin.range-check | io, C |
| 14 | while-loop | `while`, счётчик, накопление суммы, ввод до значения-ограничителя | kotlin.while, kotlin.loop-condition | io, C |
| 15 | for-loop | `for (i in 1..n)`, `until`, `downTo`, `step`, `repeat` | kotlin.for, kotlin.range, kotlin.loop-variable | io, C |
| 16 | loop-control | `break`, `continue`, вложенные циклы | kotlin.break, kotlin.continue, kotlin.nested-loops | io, C |
| **Блок 3. Функции и коллекции** |||||
| 17 | functions | `fun`, параметры, тип результата, `return`, `Unit`, выражение-тело, область видимости | kotlin.function, kotlin.parameter, kotlin.return, kotlin.unit, kotlin.scope | io (готовый `main`), C |
| 18 | nullable-values | `Int?`/`String?`, `toIntOrNull()`, `readlnOrNull()`, `?:`, `== null`, чтение до конца ввода | kotlin.nullable, kotlin.null, kotlin.toint-or-null, kotlin.elvis, kotlin.read-until-end | io, C |
| 19 | lists | `List`/`MutableList`: `listOf`, `mutableListOf`, `add`, `[i]`, `size`, `contains`, обход `for`, `sorted()`, `sum()` | kotlin.list, kotlin.mutable-list, kotlin.list-index, kotlin.for-each-element | io, C |
| 20 | maps | `Map`/`MutableMap`: `mapOf`, `mutableMapOf`, `[key]`, `containsKey`, `getOrDefault`, обход пар | kotlin.map, kotlin.key-value, kotlin.map-iteration | io, C |
| 21 | string-methods | `split`, `trim`, `substring`, `indexOf`, `contains`, `replace`, `startsWith`, `joinToString` | kotlin.string-methods, kotlin.split | io, C |
| **Блок 4. Надёжность и мост к first-steps** |||||
| 22 | exceptions | `try/catch`, `NumberFormatException`, `throw IllegalArgumentException(...)`, проверка входных данных | kotlin.exception, kotlin.try-catch, kotlin.throw | io, C |
| 23 | first-class | первый `class` и `data class`: свойства в конструкторе, метод, `this`, `println(объект)` | kotlin.class, kotlin.property, kotlin.constructor, kotlin.object, kotlin.data-class | io, C |
| 24 | mini-project | мини-проект: ведомость оценок из строк `имя;балл` (плохие строки считаются и пропускаются) | - | io, D |

Правила для последующих уроков (12-24):

* Уроки 12-16 используют числа, символы и логические значения в условиях; сравнение строк через `==` встречается с урока 11
  (там оговорено, что в Kotlin оно сравнивает содержимое).
* Урок 14 читает «пока не встретится ограничитель» (например `0`), урок 18 вводит чтение «пока есть данные»
  (`readlnOrNull()`), и с него упражнения принимают ввод разной длины.
* С урока 17 заготовка содержит готовый `main`, который читает ввод и вызывает функцию ученика.
* Урок 23 - мост: пример `Item`/`Student` в том же файле `Main.kt`; `class` объясняется как «описание своего типа
  данных». Следующий курс начинается с `kotlinc` и JVM.
* Урок 24 использует `split(";")`, `toIntOrNull()`, `try/catch` или `?:`, `MutableMap`/`MutableList` и `data class`.

## kotlin-for-programmers (уровень 1)

Id `kotlin.programmers.<slug>`; упражнение - io с готовым `main`, квиз на ловушки Kotlin (целочисленное деление и
переполнение `Int`, отсутствие неявных преобразований `Int`/`Long`/`Double`, `val` не делает коллекцию неизменяемой,
`List` против `MutableList`, `map[key]` возвращает `V?`, `toInt()` против `toIntOrNull()`, форматирование и `Locale`).

| # | slug | assessmentFor (`kotlin.from-zero.*`) |
|---|---|---|
| 01 | syntax-types-and-control-flow | 01-16 |
| 02 | functions-collections-and-strings | 17, 19, 20, 21 |
| 03 | nullability-exceptions-and-classes | 18, 22, 23 |

Урок 24 (мини-проект) в зачёт не входит.

## kotlin-skill-check

Id `kotlin.check.<slug>`; урок-проверка = упражнение (io) + квиз на чтение кода.

| Компетенция | slug | assessmentFor |
|---|---|---|
| Основы: значения, ввод, условия, циклы | basics | from-zero 01-16 |
| Функции, списки, словари, строки | collections | from-zero 17, 19, 20, 21 |
| Ошибки и ввод | errors-input | from-zero 08, 09, 18, 22 |
| Null-безопасность | null-safety | kotlin.first-steps.null-safety-and-val-var, kotlin.fundamentals.types-and-null-safety |
| Классы, data-классы, sealed и `when` | classes | kotlin.fundamentals.classes-sealed-and-when |
| Коллекции и функции высшего порядка | functional-collections | kotlin.fundamentals.collections-and-sequences |
| Обработка ошибок: исключения, `Result`, `require` | error-handling | kotlin.fundamentals.error-handling |
| Корутины | coroutines | kotlin.fundamentals.concurrency-threads-and-coroutines |

## Связи

* `kotlin-first-steps`: `requires: [kotlin-from-zero]`; урок 01 (`kotlin.first-steps.how-kotlin-runs`) зависит от
  `kotlin.from-zero.first-class` (урок 23). Урок 24 (мини-проект) - необязательное завершение; его `next` - урок
  `kotlin.first-steps.how-kotlin-runs`. Цель урока 01 first-steps («написать программу, читающую числа из stdin») к этому
  моменту уже достигнута во from-zero, поэтому формулировки смещаются к `kotlinc`/JVM, `Long` и переполнению; уроки
  first-steps 01-02 не повторяют `val`/`var`, `toIntOrNull()` и `?:`, а берут их как известные (null-безопасность в 02
  начинается с `?.`, smart cast и `!!`).
* `kotlin-fundamentals`: `requires: [kotlin-first-steps]` (как сейчас).
* `kotlin-for-programmers`: последний урок ведёт в `kotlin.first-steps.how-kotlin-runs`.
