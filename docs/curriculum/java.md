# Трек Java: структура

Стандарт: [../curriculum-standards.md](../curriculum-standards.md). Эталоны: [go.md](go.md), [javascript.md](javascript.md). Маршрут: `content/routes/java.yaml`.

```
java-from-zero (уровень 0) ──┐
                             ├─> java-first-steps (как работает JVM, равенство) ─> java-fundamentals
java-for-programmers (1) ────┘     (Spring и другие курсы опираются на fundamentals)
   зачитывает уроки java-from-zero
java-skill-check: проверки компетенций
```

## Особенности Java, которые определили порядок

* **Как запускается программа.** Каждое упражнение - один файл `Main.java` (`entryFile`). Приложение компилирует его
  `javac` и запускает `java -cp ... Main` (нужен JDK; уроки рассчитаны на Java 22 и не используют preview-возможности).
  Вывод - `System.out.println`. Файлов больше одного в упражнениях from-zero нет: дополнительные классы (урок 23) лежат в том же
  `Main.java` без слова `public`.
* **Обёртка `public class Main { public static void main(String[] args) { ... } }` неизбежна.** Компактные файлы без
  класса появились только в Java 25 (в 22 они требуют `--enable-preview`), поэтому обёртка остаётся в каждой заготовке и
  каждом примере. В уроке 01 она объясняется честно одним абзацем: «обёртка, которую требует Java», а смысл слов
  `public`, `class`, `static`, `void`, `String[] args` отложен до уроков о методах (17) и первом классе (23). Для
  упражнения понимать её не нужно: правки идут внутри `main`. О том, что в новых версиях обёртку можно опустить, сказано
  в разделе «Для тех, кто уже программирует».
* **Ввод вводится рано (урок 08)**, сразу после строк, а не в конце курса, как в JavaScript: `Scanner(System.in)` в Java -
  три понятные строки без модулей и кодировок, а с вводом упражнения могут проверяться на разных данных. Слово `new`
  объясняется минимально: «создать объект-читатель»; строка `import java.util.Scanner;` объясняется как «где искать
  Scanner». До урока 08 упражнения печатают фиксированный вывод (stdin пустой).
* **Разбор дробных чисел и форматирование зависят от локали компьютера** (на русской локали `String.format("%.2f", 2.5)` даёт
  `2,50`, `Scanner.nextDouble()` ждёт запятую). Поэтому дробные числа читаются как `Double.parseDouble(in.next())`
  (точка всегда), а `printf` и `String.format` всегда вызываются с `Locale.ROOT` (урок 10). Причина объясняется.
* **Строки сравниваются `equals`, а не `==`.** До урока 21 `==` применяется только к числам и символам (в уроке 11 это
  оговорено); в уроке 21 разбирается, почему `==` для строк ненадёжен. Ссылочная семантика, пул строк, упаковка - в
  `java-first-steps` и `java-fundamentals`.
* **Функции - статические методы класса `Main`** (урок 17). `static` объясняется как «метод принадлежит классу и вызывается
  без объекта»; объекты и экземпляры - урок 23. С урока 17 заготовка содержит готовый `main`, который читает ввод и
  вызывает метод ученика, поэтому тесты меняют данные.
* **Коллекции с обобщениями** (`ArrayList<Integer>`, `HashMap<String, Integer>`) вводятся уроками 19-20. Угловые скобки
  объясняются как «тип элементов»; настоящие обобщения (подстановочные типы, ограничения) - в
  `java-fundamentals` (`generics-collections`). Массивы (урок 18) идут раньше списков, потому что они нужны для `split` и `main`.
* **`switch` - только стрелочная форма** (`case 1 -> ...`, Java 14+): без `break` и «проваливания». Старая форма с
  двоеточиями упоминается в разделе для программистов.
* **Исключения.** Урок 22 учит `try/catch` и `throw` на примере `NumberFormatException`; проверяемые исключения,
  `try-with-resources`, `finally` и иерархия - в `java-fundamentals` (`exceptions-try-with-resources`).
* **Что отложено** в `java-first-steps` / `java-fundamentals`: JVM, JIT и сборка мусора, байткод, упаковка и `Integer`-кеш,
  `==` против `equals` на объектах, интерфейсы, наследование, записи, `enum`, потоки, `Optional`, модули. Во from-zero это
  встречается только в разделах «Для тех, кто уже программирует» и «Углубление».

## java-from-zero (уровень 0)

Id `java.from-zero.<slug>`; каталог `NN-<slug>`; файл решения - `Main.java`. Все упражнения - `io`.

| # | slug | Тема | introduces | Проверка |
|---|---|---|---|---|
| **Блок 0. Знакомство** |||||
| 01 | first-program | что такое программа и компилятор; обёртка `Main`/`main`; `System.out.println` | java.program, java.compiler, java.main-wrapper, java.println, java.string-literal | io, A |
| 02 | compiler-errors | как читать сообщение `javac`: файл, строка, `^`, каскад ошибок | java.compile-error, java.error-location | io, B (исправить) |
| 03 | output | `print` и `println`, `\n`, `\"`, склейка `+`, комментарии | java.print, java.escape-sequence, java.concatenation, java.comment | io, B |
| **Блок 1. Значения** |||||
| 04 | variables | объявление, присваивание, `int`, `String` | java.variable, java.assignment, java.int, java.string | io, B |
| 05 | basic-types | `double`, `boolean`, `char`, `final`, литералы | java.type, java.double, java.boolean, java.char, java.final | io, B |
| 06 | arithmetic | `+ - * / %`, целочисленное деление, `+=` | java.arithmetic, java.integer-division, java.remainder, java.compound-assignment | io, B |
| 07 | strings | `length()`, `charAt()`, `toUpperCase()`, индексы с нуля | java.string-length, java.char-at, java.index, java.string-case | io, B |
| 08 | reading-input | `Scanner`: `next`, `nextInt`, `nextLine`; `import`; ловушка `nextLine` | java.import, java.scanner, java.new, java.next-line | io, C |
| 09 | type-conversion | `Integer.parseInt`, `Double.parseDouble`, приведение `(int)`/`(double)` | java.parse-int, java.parse-double, java.cast, java.number-format-exception | io, B |
| 10 | formatted-output | `printf`, `%d %s %.2f %n %%`, `Locale.ROOT`, `Math.round` | java.printf, java.format-specifier, java.locale, java.math-round | io, B |
| 11 | comparison-and-logic | `== != < > <= >=`, `&& \|\| !`, приоритет, короткое вычисление | java.comparison, java.logical-operators, java.boolean-expression, java.short-circuit | io, B |
| **Блок 2. Управление** |||||
| 12 | if-else | `if / else if / else`, блок `{}` | java.if, java.else, java.block | io, C |
| 13 | switch | стрелочный `switch`, `default`, несколько меток | java.switch, java.default-case | io, C |
| 14 | while-loop | `while`, счётчик, накопление суммы, чтение до конца ввода (`hasNext`) | java.while, java.loop-condition | io, C |
| 15 | for-loop | `for (int i = 0; ...)`, шаг, обратный отсчёт | java.for, java.loop-variable | io, C |
| 16 | loop-control | `break`, `continue`, вложенные циклы | java.break, java.continue, java.nested-loops | io, C |
| **Блок 3. Методы и коллекции** |||||
| 17 | methods | `static` методы, параметры, `return`, `void`, область видимости | java.method, java.parameter, java.return, java.void, java.static, java.scope | io (готовый `main`), C |
| 18 | arrays | массив, индекс, `length`, обход `for` и for-each, `ArrayIndexOutOfBoundsException` | java.array, java.array-length, java.for-each | io, C |
| 19 | arraylist | `ArrayList<T>`: `add`, `get`, `size`, `contains`, `remove`; тип элементов в `<>` | java.arraylist, java.type-argument | io, C |
| 20 | hashmap | `HashMap<K, V>`: `put`, `get`, `containsKey`, `getOrDefault`, обход | java.hashmap, java.key-value | io, C |
| 21 | string-methods | `equals` против `==`, `trim`, `split`, `substring`, `indexOf`, `contains`, `replace` | java.equals, java.string-methods, java.split | io, C |
| **Блок 4. Надёжность и мост к first-steps** |||||
| 22 | exceptions | `try/catch`, `NumberFormatException`, `throw new ...`, проверка входных данных | java.exception, java.try-catch, java.throw | io, C |
| 23 | first-class | первый класс: поля, конструктор, метод экземпляра, `new`, `this` | java.class, java.field, java.constructor, java.object, java.this | io, C |
| 24 | mini-project | мини-проект: ведомость оценок из строк `имя;балл` (плохие строки считаются и пропускаются) | - | io, D |

Правила для последующих уроков (12-24):

* Уроки 12-16 используют только числа и символы в условиях; сравнение строк - с урока 21.
* Урок 14 вводит чтение «пока есть данные» (`while (in.hasNextInt())` / `hasNextLine`), чтобы упражнения принимали
  ввод разной длины.
* С урока 17 заготовка содержит готовый `main`; ученик пишет `static`-методы.
* Урок 23 - мост: пример `Item`/`Student` с полями и конструктором в том же файле `Main.java`; смысл слов `public`, `class`,
  `static` из обёртки теперь объясняется полностью (на него ссылается урок 01). Следующий курс начинается с JVM.
* Урок 24 использует `split(";")`, `Integer.parseInt` в `try/catch`, `HashMap`/`ArrayList` и класс из урока 23.

## java-for-programmers (уровень 1)

Id `java.programmers.<slug>`; упражнение - io с готовым `main`, квиз на ловушки Java (целочисленное деление и
переполнение `int`, `==` против `equals`, `char` + `int`, `nextLine` после `nextInt`, локаль в `printf`/`nextDouble`,
`Integer`-кеш и `null`, индекс за границей массива).

| # | slug | assessmentFor (`java.from-zero.*`) |
|---|---|---|
| 01 | syntax-types-and-control-flow | 01-16 |
| 02 | methods-collections-and-strings | 17-21 |
| 03 | exceptions-and-classes | 22-23 |

Урок 24 (мини-проект) в зачёт не входит.

## java-skill-check

Id `java.check.<slug>`; урок-проверка = упражнение (io) + квиз на чтение кода.

| Компетенция | slug | assessmentFor |
|---|---|---|
| Основы: значения, ввод, условия, циклы | basics | from-zero 01-16 |
| Методы, массивы, списки, словари, строки | collections | from-zero 17-21 |
| Ошибки и ввод | errors-input | from-zero 08, 09, 22 |
| Равенство: `==` против `equals`, упаковка | equality | java.first-steps.primitives-objects-and-equality |
| Классы, записи, перечисления | classes | java.fundamentals.classes-records-enums |
| Обобщения и коллекции | generics | java.fundamentals.generics-collections |
| Лямбды, Streams, Optional | streams | java.fundamentals.streams-lambdas-optional |
| Потоки и конкурентность | concurrency | java.fundamentals.threads-executors-virtual |

## Связи

* `java-first-steps`: `requires: [java-from-zero]`; урок 01 (`java.first-steps.how-java-runs`) зависит от
  `java.from-zero.first-class` (урок 23). Урок 24 (мини-проект) - необязательное завершение; его `next` - урок
  `java.first-steps.how-java-runs`. Первая цель урока 01 first-steps («написать программу, читающую числа через Scanner»)
  к этому моменту уже достигнута во from-zero, поэтому формулировки первого урока first-steps нужно сместить к JVM.
* `java-fundamentals`: `requires: [java-first-steps]` (как сейчас).
* `java-for-programmers`: последний урок ведёт в `java.first-steps.how-java-runs`.
