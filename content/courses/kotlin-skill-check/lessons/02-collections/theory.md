<!-- section: simple -->
## Что проверяет эта проверка

Функции с параметрами и результатом, `List` и `MutableList`, `Map` и `MutableMap`, обход коллекций, `sorted()`, `sum()`, строковые методы `split`, `trim`, `substring`, `indexOf`, `contains`, `replace`, `joinToString`.

## Что даёт прохождение

Проверка состоит из короткого упражнения с запуском тестов и квиза на чтение кода (порог квиза - 75%). Если пройдены обе части, засчитываются уроки: [Функции](lesson:kotlin.from-zero.functions), [Списки](lesson:kotlin.from-zero.lists), [Словари](lesson:kotlin.from-zero.maps), [Методы строк](lesson:kotlin.from-zero.string-methods).

Если не пройдены, ничего не отнимается: прогресс сохраняется, а система покажет первый незавершённый урок из этого списка как место для повторения. Проверку можно пройти снова в любой момент, и открыть её можно без каких-либо условий.

<!-- section: examples -->
## Пример того, что понадобится

```kotlin run
fun total(prices: List<Int>): Int {
    var sum = 0
    for (p in prices) {
        sum += p
    }
    return sum
}

fun main() {
    val counts = mutableMapOf<String, Int>()
    for (w in "b a b c a b".split(" ")) {
        counts[w] = counts.getOrDefault(w, 0) + 1
    }
    println(counts)
    for (k in counts.keys.sorted()) {
        println("$k=${counts[k]}")
    }
    println(total(listOf(5, 10, 20)))
    println(listOf("x", "y", "z").joinToString("-"))
    println("  Привет, мир  ".trim().substring(0, 6))
}
```
```output
{b=3, a=2, c=1}
a=2
b=3
c=1
35
x-y-z
Привет
```
Словарь накапливает счётчики по ключу: `getOrDefault` возвращает ноль, если ключа ещё нет. Ключи сортируются отдельно (`keys.sorted()`), потому что `mutableMapOf` хранит ключи в порядке вставки. `joinToString` собирает список в строку, а `substring(0, 6)` берёт шесть символов - правая граница не включается.

<!-- section: precise -->
## Что нужно уметь

* объявлять функции с параметрами и типом результата и вызывать их из `main`;
* создавать `mutableListOf` и `mutableMapOf`, добавлять элементы и читать их по индексу или ключу;
* помнить, что `map[key]` возвращает `V?`, и использовать `getOrDefault`, `?:` или проверку на `null`;
* обходить словарь и сортировать ключи или значения через `sorted()`;
* разбирать строку через `split`, обрезать пробелы через `trim` и преобразовывать части через `toInt()`;
* помнить, что `substring(from, to)` не включает `to`, а индексы начинаются с нуля.

<!-- section: mistakes -->
## На чём чаще всего ошибаются

* Обращение к `list[i]` за границей списка бросает исключение, а не возвращает значение по умолчанию.
* `map[key]` для отсутствующего ключа возвращает `null`; его нельзя присвоить переменной типа `Int`.
* `xs.sorted()` возвращает новый список и не меняет `xs`; для изменения на месте используют `sort()` у `MutableList`.
* `split(" ")` оставляет пустые элементы при двойных пробелах.
* Порядок ключей `mutableMapOf` - порядок вставки, а не алфавитный.

<!-- section: summary -->
## Итог

Если задания показались трудными, начните с уроков из списка: [Функции](lesson:kotlin.from-zero.functions), [Списки](lesson:kotlin.from-zero.lists), [Словари](lesson:kotlin.from-zero.maps), [Методы строк](lesson:kotlin.from-zero.string-methods). После повторения вернитесь к проверке: результат не зависит от числа попыток, а засчитанное остаётся засчитанным.
