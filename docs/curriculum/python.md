# Трек Python: структура

Стандарт: [../curriculum-standards.md](../curriculum-standards.md). Маршрут: `content/routes/python.yaml`.
Аудит исходного состояния: [../curriculum-audit.md](../curriculum-audit.md) (списки во 2-м уроке, `def` в 4-м,
`class` в 10-м, CPython и `compile`/`exec` в 1-м).

Python - не Go: здесь нет компиляции в двоичный файл и строгих объявлений, зато есть динамическая типизация,
`input()` возвращает строку, отступы - часть синтаксиса, а ошибки обнаруживаются при выполнении. Поэтому ввод данных
появляется рано (урок 02), а преобразование строки в число (урок 03) - сразу за ним; разбор ошибок опирается на
traceback, а не на сообщения компилятора.

## Совместимость

Id существующих уроков **сохраняются**: на них ссылаются 15+ курсов и 7 путей, а у учеников есть прогресс. Существующий
урок переписывается под стандарт, если его умение остаётся тем же или сужается (зачтённое раньше остаётся честным);
новые темы получают новые id. Каталоги перенумеровываются (`git mv`), id внутри не меняются.

## python-fundamentals (уровень 0)

| # | id (`python.fundamentals.*`) | Статус | Тема | introduces |
|---|---|---|---|---|
| 01 | how-python-runs | переписать | первая программа: `print`, строки, запуск; CPython и байткод - в deep-dive | python.program, python.print, python.string-literal |
| 02 | variables-and-input | переписать (без списков) | переменная, присваивание, `input()` | python.variable, python.assignment, python.input |
| 03 | numbers-and-operators | переписать | `int`, `float`, арифметика, `//`, `%`, `int(input())` | python.int, python.float, python.arithmetic, python.type-conversion |
| 04 | text-and-f-strings | новый | конкатенация, f-строки, `len`, форматирование `:.2f` | python.f-string, python.len |
| 05 | reading-errors | новый | traceback: как читать `NameError`, `TypeError`, `ValueError`, `SyntaxError`, `IndentationError` | python.traceback |
| 06 | conditions | переписать (без `def`) | `bool`, сравнения, `if/elif/else`, `and/or/not`, отступы | python.bool, python.comparison, python.if, python.logical-operators |
| 07 | while-loops | новый | `while`, счётчик, `break`, `continue`, ввод до стоп-слова | python.while, python.break-continue |
| 08 | loops | переписать | `for` и `range`, вложенные циклы | python.for-range, python.nested-loops |
| 09 | functions | переписать (easy) | `def`, параметры, `return`, область видимости | python.def, python.parameter, python.return, python.scope |
| 10 | strings | переписать | индексы, срезы, методы строк, `in`, обход строки | python.string-index, python.slice, python.string-methods |
| 11 | lists | переписать | список, `append`, индексы, обход, `sum/min/max`, `sorted` | python.list, python.list-methods |
| 12 | dicts-and-sets | переписать | словарь, `get`, обход `items()`, множество | python.dict, python.set |
| 13 | errors-and-files | переписать (без `class`) | `try/except`, `raise`, `with open`, чтение строк | python.try-except, python.raise, python.file-io |
| 14 | modules | новый | `import`, `from ... import`, `math`, `random` с seed, свой модуль | python.import |
| 15 | mini-project | новый | мини-проект: дневник тренировок из текстового файла / ввода | - |

Хвосты: CPython, байткод, объектная модель, ссылки, GC, GIL - в `deep-dive` соответствующих уроков (только для
интересующихся) и далее в `python-advanced` / `python-concurrency`.

## python-for-programmers (уровень 1)

Id `python.programmers.*`; плотные уроки с упражнением (harness) и квизом на ловушки Python для тех, кто знает другой
язык (динамическая типизация, отступы, изменяемость, `is` и `==`, целые произвольной длины, итерация вместо индексов).

| # | slug | assessmentFor (`python.fundamentals.*`) |
|---|---|---|
| 01 | syntax-and-control-flow | 01-08 |
| 02 | functions-and-strings | 09, 10 |
| 03 | collections | 11, 12 |
| 04 | errors-files-modules | 13, 14 |

## python-skill-check

Id `python.check.*`; урок-проверка = упражнение (harness) + квиз на чтение кода.

| Компетенция | slug | assessmentFor |
|---|---|---|
| Основы: переменные, ввод, условия, циклы | basics | fundamentals 01-08 |
| Функции и строки | functions | fundamentals 09, 10 |
| Списки и словари | collections | fundamentals 11, 12 |
| Исключения, файлы, модули | errors | fundamentals 13, 14 |
| Функции как объекты, декораторы | decorators | python.advanced.functions-as-objects, decorators |
| Итераторы и генераторы | iterators | python.advanced.iterators-and-generators |
| Классы и протоколы | classes | python.advanced.classes-and-objects, dunder-methods-and-dataclasses, inheritance-composition-protocols |
| Аннотации типов | typing | python.advanced.typing-fundamentals |

Профессиональные темы (AsyncIO, multiprocessing, профилирование, память, packaging) уже частично есть в
`python-concurrency`, `python-stdlib`, `python-advanced`; их доработка под стандарт - следующий этап (см. ROADMAP).
