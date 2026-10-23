# Трек Go: структура (эталон стандарта)

Стандарт: [../curriculum-standards.md](../curriculum-standards.md). Маршрут и проверки: `content/routes/go.yaml`.
Это рабочий план; источник истины - файлы курсов.

```
go-first-steps (уровень 0, с нуля) ──┐
                                     ├─> go-fundamentals (язык и идиомы) ──> go-professional (уровень 4)
go-for-programmers (уровень 1) ──────┘          (go-ml-gomlx, ML на Go, тоже после fundamentals)
   зачитывает уроки go-first-steps
go-skill-check: уроки-проверки компетенций (assessmentFor), порядок - content/routes/go.yaml
```

`go-fundamentals` требует `go-first-steps` через `requires`; программист проходит `go-for-programmers`, чьи уроки
зачитывают уроки `go-first-steps` (`assessmentFor`), и попадает в `go-fundamentals` без повторения основ.

## go-first-steps (уровень 0)

Id урока: `go.first-steps.<slug>`; каталог `NN-<slug>`. В `concepts` используются идентификаторы из столбцов.

| # | slug | Тема | introduces | Упражнение |
|---|---|---|---|---|
| **Блок 0. Знакомство** |||||
| 01 | first-program | Что такое программа; Go; первая программа | go.program, go.package-main, go.func-main, go.import, go.println, go.string-literal | A: изменить выводимую строку (io) |
| 02 | compiler-errors | Как читать сообщения об ошибках; сборка и запуск | go.compile-error, go.build-and-run | B: исправить три ошибки в программе (io) |
| **Блок 1. Базовый синтаксис** |||||
| 03 | output | Println с несколькими значениями, Print, перевод строки | go.print-values, go.newline | B: вывести чек из строк и чисел (io) |
| 04 | variables | Переменные: var, :=, присваивание | go.variable, go.short-declaration, go.assignment | B: переменная name и приветствие (io) |
| 05 | basic-types | int, float64, string, bool; нулевые значения; строгая типизация | go.type, go.int, go.float64, go.string, go.bool, go.zero-value | B: объявить переменные нужных типов и вывести (io) |
| 06 | arithmetic | + - * / %, целочисленное деление, приоритет | go.arithmetic, go.integer-division, go.remainder | B: минуты в часы и минуты по фиксированному значению (io) |
| 07 | reading-input | fmt.Scan: чтение чисел и слов | go.scan | C: прочитать два числа, вывести сумму (io) |
| 08 | type-conversion | float64(x), int(f), почему нельзя смешивать типы | go.type-conversion | C: среднее трёх целых (io) |
| 09 | formatted-output | fmt.Printf и Sprintf: %d %s %.2f %v | go.printf | C: строка чека с ценой с двумя знаками (io) |
| 10 | constants-and-comments | const, комментарии, имена | go.const, go.comment | B: цена с НДС через константу (io) |
| **Блок 2. Управление выполнением** |||||
| 11 | comparison-and-logic | сравнения, bool, && \|\| ! | go.comparison, go.logical-operators | B: проверки возраста и суммы (io) |
| 12 | if-else | if / else if / else | go.if | C: оценка по баллам (io) |
| 13 | switch | switch по значению и без выражения | go.switch | C: номер дня недели → название (io) |
| 14 | for-loop | for i := ...; i <= n; i++ | go.for-loop | C: сумма 1..n и вывод ряда (io) |
| 15 | loop-control | for с условием, бесконечный for, break, continue | go.for-condition, go.break-continue | C: суммировать числа до 0, пропуская отрицательные (io) |
| 16 | nested-loops | вложенные циклы | go.nested-loops | C: таблица умножения n×n (io) |
| **Блок 3. Функции** |||||
| 17 | functions | зачем функции, объявление, параметры, вызов | go.func, go.parameter | B: функция приветствия; проверка `go test` вызывает функцию |
| 18 | return-values | return, несколько возвращаемых значений | go.return, go.multiple-returns | C: minMax, деление с остатком (go test) |
| 19 | scope | блоки, область видимости, затенение | go.scope, go.shadowing | B: найти и исправить ошибку затенения (go test) |
| 20 | errors-first-look | ошибка как значение: strconv.Atoi, if err != nil, errors.New | go.error-value, go.strconv-atoi, go.errors-new | C: parseAge с проверкой диапазона (go test) |
| **Блок 4. Коллекции** |||||
| 21 | arrays | массив фиксированной длины, индекс, len, выход за границы | go.array, go.index, go.len | C: температуры за неделю: максимум и его день (io) |
| 22 | slices | срез, литерал, append, len | go.slice, go.append | C: прочитать n чисел, вывести в обратном порядке (io) |
| 23 | range | for i, v := range | go.range | C: сколько значений выше среднего (io) |
| 24 | slice-algorithms | поиск, фильтрация в новый срез, сортировка slices.Sort | go.filter, go.slices-sort | C: отобрать и отсортировать (io) |
| 25 | maps | map: создание, чтение, comma-ok, delete, обход и порядок | go.map, go.comma-ok, go.sorted-keys | C: частота слов до конца ввода (io) |
| 26 | strings-and-runes | байты и руны, range по строке, пакет strings | go.rune, go.strings-package | C: статистика слова с кириллицей (go test) |
| **Блок 5. Практика** |||||
| 27 | number-stream | bufio.Reader, fmt.Fscan, int64 и переполнение, статистика | go.bufio-reader, go.int64-overflow | C (formerIds: `go.first-steps.how-go-runs`, те же тесты) |
| 28 | text-lines | bufio.Scanner построчно, strings.Fields, разбор строк | go.bufio-scanner | C: ведомость оценок (io) |
| 29 | mini-project-expenses | мини-проект: учёт расходов | - | D: итоги по категориям, плохие строки (io) |

Блоки 6-9 (структуры, методы, указатели, интерфейсы, ошибки с контекстом, пакеты и модули, gofmt и go test,
конкурентность, context) - курс `go-fundamentals`; блок 9 - `go-professional`.

## go-for-programmers (уровень 1)

Id `go.programmers.<slug>`. Каждый урок - плотное изложение отличий Go от других языков, упражнение `go test`, квиз
на типичные ловушки; прохождение зачитывает уроки `go-first-steps`.

| # | slug | Тема | assessmentFor (go.first-steps.*) |
|---|---|---|---|
| 01 | syntax-and-control-flow | пакеты, переменные, типы, нулевые значения, for/if/switch, ввод-вывод | 01-16 |
| 02 | functions-and-errors | функции, несколько значений, ошибки-значения, области видимости | 17-20 |
| 03 | collections-and-strings | массивы, срезы, append, range, map, руны и strings | 21-26 |
| 04 | streams-and-text | bufio, Fscan, Scanner, int64 | 27-28 |

## go-fundamentals (уровень 1)

Существующие уроки 01-08 (типы, функции и ошибки, структуры и методы, интерфейсы, карты и срезы подробно, горутины и
каналы, context, пакеты/модули/тесты) + 09 `go.fundamentals.errors-and-table-tests` (бывший
`go.first-steps.packages-and-go-test`, содержание без изменений, `formerIds`). Первый урок зависит от
`go.first-steps.strings-and-runes`.

## go-professional (уровень 4)

Id `go.pro.<slug>`; требует `go-fundamentals`.

| # | slug | Тема | Проверка |
|---|---|---|---|
| 01 | slice-internals | устройство среза, общий массив, append (бывший `go.first-steps.slices-len-cap-and-append`, `formerIds`) | io |
| 02 | allocations | аллокации, strings.Builder, предвыделение, бенчмарки | `testing.AllocsPerRun` + корректность |
| 03 | data-races | гонки данных, sync.Mutex, атомарные операции, race detector | конкурентные тесты инвариантов |
| 04 | memory-model | happens-before, публикация значения, sync.Once | тесты публикации |
| 05 | context-cancellation | отмена, дедлайны, утечки горутин | тест завершения и отсутствия зависших горутин |
| 06 | resource-cleanup | defer, Close на всех путях, errors.Join | фейковые ресурсы считают Close |
| 07 | tests-that-catch-bugs | набор кейсов, отличающий корректную реализацию от ошибочных | мутанты в скрытых тестах |
| 08 | escape-analysis-and-gc | куча и стек, escape analysis, GC и давление на него | AllocsPerRun + квиз по выводу `-gcflags=-m` |

## go-skill-check

Id `go.check.<slug>`; урок-проверка = короткое упражнение (`go test`) + квиз на чтение кода. Порядок и `after` - в
`content/routes/go.yaml`.

| Компетенция | slug | assessmentFor |
|---|---|---|
| Синтаксис, типы, условия и циклы | basics | go.first-steps 01-16 |
| Функции и ошибки-значения | functions | go.first-steps 17-20 |
| Срезы, карты, строки | collections | go.first-steps 21-26 |
| Потоковый ввод | input | go.first-steps 27-28 |
| Структуры, методы, указатели | structs | go.fundamentals.types-values-zero-values, structs-methods-embedding |
| Интерфейсы | interfaces | go.fundamentals.interfaces |
| Обработка ошибок | errors | go.fundamentals.functions-and-errors, errors-and-table-tests |
| Тестирование и модули | testing | go.fundamentals.packages-modules-testing |
| Горутины и каналы | concurrency | go.fundamentals.goroutines-and-channels |
| Context и отмена | context | go.fundamentals.concurrency-patterns-and-context, go.pro.context-cancellation |
| Срезы и память изнутри | internals | go.fundamentals.maps-and-slices-deep-dive, go.pro.slice-internals |
