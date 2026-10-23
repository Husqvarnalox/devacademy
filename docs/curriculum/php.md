# Трек PHP: структура

Стандарт: [../curriculum-standards.md](../curriculum-standards.md). Эталоны: [javascript.md](javascript.md), [go.md](go.md). Маршрут: `content/routes/php.yaml`.

```
php-from-zero (уровень 0) ──┐
                            ├─> php-first-steps (Zend Engine, опкоды, массивы-хеш-таблицы) ─> php-fundamentals
php-for-programmers (1) ────┘
   зачитывает уроки php-from-zero
php-skill-check: проверки компетенций
```

## Особенности PHP, которые определили порядок

* **PHP здесь - язык командной строки.** Приложение запускает `php main.php` с фиксированными настройками (`Runtimes.cs`,
  `PhpRuntime.Settings`): ошибки и предупреждения идут в **stderr**, а в stdout попадает только то, что напечатала программа;
  `precision=14`, `serialize_precision=-1`, `default_charset=UTF-8`, OPcache в CLI выключен. Веб-сервер, формы, HTML-шаблоны,
  `$_GET` и сессии в треке не используются; урок 01 говорит об этом прямо.
* **`<?php` - единственная неустранимая строка.** Файл без неё PHP выводит как обычный текст. Урок 01 даёт честное
  объяснение в одну фразу («так PHP отличает код от текста; подробно - в `php-first-steps`»). Закрывающий тег `?>` не пишется
  никогда. Текст вне тегов, устройство Zend Engine, опкоды, OPcache и JIT остаются в разделе `deep-dive` урока 01 и в
  `php-first-steps` (урок 01): здесь они не дублируются.
* **`declare(strict_types=1);`** вводится в уроке про функции (16), когда у значений появляются объявленные типы.
  До этого он не влияет на программу (строгий режим касается только вызовов функций и `return`). С урока 16 каждое упражнение
  начинается с `<?php` и `declare(strict_types=1);`. Причина: нестрогий режим молча превращает `"5"` в `5` и `"5 штук"` в
  ошибку или предупреждение, а ошибка типа на границе функции находится сразу. Арифметика и сравнение строгим режимом **не**
  затрагиваются; это отдельно объясняется в уроках 09 и 11.
* **`===`, а не `==`.** Урок 11 учит `===`/`!==` как единственное равенство курса и показывает, почему `==` в PHP
  непредсказуем (`"1" == "01"`, `"10" == "1e1"`, `null == false`, `[] == false`). В PHP 8 `0 == "abc"` ложно, но правило
  «число с нечисловой строкой» всё равно не стоит держать в голове.
* **Вывод:** `echo` и `"\n"` (не `PHP_EOL`: в CLI тесты сравнивают `\n`), подстановка `"{$x}"`, склейка `.`. Форматирование:
  `printf("%.2F")` и `number_format($x, 2, '.', '')`. Заглавная `F` не зависит от локали; в `number_format` разделители заданы
  явно. Нюанс округления: `number_format`/`round` округляют «как в школе» (0.125 -> 0.13), а `printf("%.2F")` смотрит на
  двоичное значение (0.125 -> 0.12); упражнения избегают точных «половинок».
* **Кириллица и UTF-8.** `strlen` считает **байты** (`strlen("Привет")` = 12). Урок 07 вводит `mb_strlen`, `mb_strtoupper`,
  `mb_substr` (расширение mbstring; в Debian/Ubuntu ставится пакетом `php-mbstring`, в macOS и официальных сборках есть),
  `strtoupper` не трогает кириллицу. `printf("%-10s")` и `str_pad` тоже считают байты - в уроке 10 указано, что колонки с
  кириллицей строят через `mb_str_pad` (PHP 8.3+, курс требует 8.4+).
* **Ввод** приходит рано: урок 08 (`fgets(STDIN)`, `trim`). До него упражнения проверяют фиксированный вывод при пустом
  stdin. Один приём чтения до конца ввода выбран и объяснён: **`while (($line = fgets(STDIN)) !== false)`** (урок 21):
  построчная обработка, без загрузки всего ввода и без регулярных выражений. `stream_get_contents`, `preg_split`,
  `file()` остаются в `php-first-steps`/`php-fundamentals`. `fgets` возвращает строку **с** `\n` (поэтому `trim`) и `false` в
  конце ввода (поэтому строгое сравнение `!== false`: пустая строка `"0"` тоже ложна).
* **Приведение и проверка ввода.** `(int)`, `(float)`, `intval` молча дают `0` для мусора; проверка - `is_numeric` (десятичное
  число, пробелы по краям допустимы) и `filter_var($s, FILTER_VALIDATE_INT) !== false` (только целое; обязательно строгое
  сравнение с `false`, потому что результат `0` - валидное число). Десятичная запятая (`3,5`) числом не считается.
* **Функции -> `main` в заготовке.** В PHP-упражнениях нет harness: после урока 16 заготовка содержит `declare(strict_types=1)`,
  заготовку функции (её дописывает ученик) и **готовый код ниже**, который читает ввод и вызывает функцию. Тесты - `io`, ввод
  меняется, поэтому функцию нельзя подогнать под один случай.
* **Массивы** - главная коллекция: списки (17-18) и ассоциативные (19) отдельными уроками, `foreach`, `count`, `in_array`,
  `array_key_exists`/`isset`/`??`, `sort`/`ksort`/`asort`. Стрелочные функции, `array_map`, `array_filter`, замыкания,
  деструктуризация и `readonly` - в `php-fundamentals` (там же `array_find`, `match`, перечисления).
* **Ошибки:** `throw new InvalidArgumentException`, `try/catch` (22). Иерархия `Throwable`, `finally`, `match` - в
  `php-fundamentals` 06. **Мостик к классам** (23): один `class` с типизированными свойствами, конструктором с продвижением
  параметров и методом; наследование, интерфейсы, `readonly`, перечисления - `php-fundamentals` 04-05.
* **Отложено в `php-first-steps`/`php-fundamentals`:** Zend Engine, опкоды, OPcache, JIT, `int`-переполнение, `stream_get_contents`,
  упорядоченная хеш-таблица и нормализация ключей, копирование массивов и ссылки (`&`), `static`/`global`, замыкания,
  пространства имён, автозагрузка, генераторы, `sprintf` с позиционными аргументами, Юникод в глубину (`intl`).

## php-from-zero (уровень 0)

Id `php.from-zero.<slug>`; каталог `NN-<slug>`; файл решения - `main.php` (io). Версия PHP - 8.4+ (проверено на 8.5).

| # | slug | Тема | introduces | Проверка |
|---|---|---|---|---|
| **Блок 0. Знакомство** |||||
| 01 | first-program | что такое программа; PHP в командной строке; `<?php`; `echo` | php.program, php.open-tag, php.echo, php.string-literal | io, A |
| 02 | reading-errors | Parse error, Fatal error, Warning: как читать сообщение и номер строки | php.error-message | io, B (исправить) |
| **Блок 1. Значения** |||||
| 03 | output | `echo` с несколькими значениями, `\n`, `\t`, одинарные и двойные кавычки, комментарии | php.escape-sequence, php.single-quoted-string, php.comment | io, B |
| 04 | variables | `$имя`, присваивание, подстановка `"{$x}"` | php.variable, php.assignment, php.interpolation | io, B |
| 05 | values-and-types | int, float, string, bool, null; `var_dump`, `get_debug_type` | php.int, php.float, php.string, php.bool, php.null, php.var-dump | io, B |
| 06 | arithmetic | `+ - * / % **`, `intdiv`, `round/floor/ceil`, `+=`, неточность дробей | php.arithmetic, php.intdiv, php.rounding, php.compound-assignment | io, B |
| 07 | strings | склейка `.`, `strlen` и байты, `mb_strlen`, `mb_strtoupper` | php.concatenation, php.strlen, php.mb-string | io, B |
| **Блок 2. Ввод и данные** |||||
| 08 | reading-input | `STDIN`, `fgets`, `trim` | php.stdin, php.fgets, php.trim | io, C |
| 09 | type-conversion | `(int)`, `(float)`, `is_numeric`, строки в арифметике | php.cast, php.is-numeric, php.type-juggling | io, C |
| 10 | formatted-output | `printf`, `%d %s %.2F`, ширина, `number_format` | php.printf, php.number-format | io, C |
| **Блок 3. Управление выполнением** |||||
| 11 | comparison-and-logic | `=== !== < > <= >=`, `&& \|\| !`, почему не `==` | php.comparison, php.strict-equality, php.logical-operators | io, B |
| 12 | if-else | `if / elseif / else` | php.if | io, C |
| 13 | while-loop | `while`, счётчик, накопление | php.while | io, C |
| 14 | for-loop | `for ($i = ...; ...; $i++)` | php.for | io, C |
| 15 | loop-control | `break`, `continue`, вложенные циклы | php.break-continue, php.nested-loops | io, C |
| **Блок 4. Функции** |||||
| 16 | functions | `function`, параметры и типы, `return`, значения по умолчанию, `declare(strict_types=1)`, локальная область | php.function, php.parameter, php.return, php.type-declaration, php.strict-types, php.default-parameter, php.local-scope | io (given main), B |
| **Блок 5. Коллекции** |||||
| 17 | arrays | список: литерал, индекс, `[]=`, `count`, `foreach`, `Undefined array key` | php.array, php.index, php.append, php.count, php.foreach | io (given main), C |
| 18 | array-functions | `in_array`, `array_sum`, `min/max`, `sort`, `implode`, `array_slice` | php.array-functions, php.sort | io, C |
| 19 | associative-arrays | `ключ => значение`, `isset`, `??`, `array_key_exists`, `ksort`/`arsort`, массив массивов | php.assoc-array, php.isset, php.null-coalescing, php.nested-array | io, C |
| 20 | string-functions | `explode`, `str_contains`, `str_replace`, `substr`/`mb_substr`, `ucfirst`, `str_pad` | php.string-functions | io, C |
| **Блок 6. Надёжность и практика** |||||
| 21 | reading-all-input | `while (($line = fgets(STDIN)) !== false)`, пустые и плохие строки | php.read-lines | io, C |
| 22 | exceptions | `throw new InvalidArgumentException`, `try/catch`, `getMessage()` | php.exception, php.throw, php.try-catch | io (given main), C |
| 23 | classes-intro | первый `class`: типизированные свойства, конструктор с продвижением, метод, `new`, `->` | php.class, php.property, php.constructor-promotion, php.method, php.new | io (given main), C |
| 24 | mini-project | мини-проект: учёт покупок из строк ввода (итоги по категориям, плохие строки) | - | io, D |

Порядок `uses`: каждое понятие вводится в одном из предыдущих уроков; предварительно проверяется валидатором.

## php-for-programmers (уровень 1)

Id `php.programmers.<slug>`; упражнение - `io` с функциями, которые вызывает готовый код; квиз на ловушки PHP (`==` и
приведение типов, `strlen` и UTF-8, `fgets` с `\n` и `false`, `0` как результат `filter_var`, `count`/`foreach` по копии, `"abc" + 1`
бросает `TypeError`, `$x = a or b`, функции не видят глобальные переменные, нестрогий режим превращает `"5"` в `5`).

| # | slug | assessmentFor (`php.from-zero.*`) |
|---|---|---|
| 01 | syntax-and-control-flow | 01-15 |
| 02 | functions-and-collections | 16-20 |
| 03 | input-errors-and-classes | 21-23 |

## php-skill-check

Id `php.check.<slug>`; урок-проверка = упражнение (io) + квиз на чтение кода (порог 0.75).

| Компетенция | slug | assessmentFor |
|---|---|---|
| Основы: значения, ввод, условия, циклы | basics | from-zero 01-16 |
| Массивы, ассоциативные массивы, строки | collections | from-zero 17-20 |
| Чтение ввода, исключения | errors-input | from-zero 21, 22 |
| Типы, strict_types, приведение, равенство | types | php.first-steps.arrays-and-strict-types, php.fundamentals.types-and-juggling |
| Массивы и функции высшего порядка | array-functions | php.fundamentals.arrays-and-functions |
| Классы, интерфейсы, перечисления | classes | php.from-zero.classes-intro, php.fundamentals.classes-readonly-enums, php.fundamentals.interfaces-traits-lsb |
| Исключения и `match` | exceptions | php.fundamentals.errors-and-match |
| Строки, форматирование и ввод-вывод | strings-io | php.fundamentals.strings-and-io, php.first-steps.how-php-runs |

## Связи

* `php-first-steps`: `requires: [php-from-zero]`; урок 01 (`php.first-steps.how-php-runs`) зависит от
  `php.from-zero.classes-intro` (последний основной урок перед мини-проектом, «мост» к первым шагам); следующий шаг последнего
  урока `php-from-zero` (мини-проект) - `php.first-steps.how-php-runs`.
* `php-fundamentals`: `requires: [php-first-steps]` (без изменений).
* Написаны все 24 урока (фазы A и B); `next` урока 11 - `php.from-zero.if-else`.
