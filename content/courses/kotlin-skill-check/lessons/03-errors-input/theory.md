<!-- section: simple -->
## Что проверяет эта проверка

Чтение строк через `readln()` и `readlnOrNull()`, преобразование `toInt()` и `toIntOrNull()`, исключение `NumberFormatException`, `try / catch`, значения типа `Int?` и оператор `?:`, подсчёт и пропуск неверных строк.

## Что даёт прохождение

Проверка состоит из короткого упражнения с запуском тестов и квиза на чтение кода (порог квиза - 75%). Если пройдены обе части, засчитываются уроки: [Ввод данных: readln](lesson:kotlin.from-zero.reading-input), [Преобразование типов](lesson:kotlin.from-zero.type-conversion), [Значения, которых может не быть](lesson:kotlin.from-zero.nullable-values), [Исключения](lesson:kotlin.from-zero.exceptions).

Если не пройдены, ничего не отнимается: прогресс сохраняется, а система покажет первый незавершённый урок из этого списка как место для повторения. Проверку можно пройти снова в любой момент, и открыть её можно без каких-либо условий.

<!-- section: examples -->
## Пример того, что понадобится

```kotlin run
fun main() {
    val lines = listOf("10", "abc", " 20 ", "7.5")
    var sum = 0
    var bad = 0
    for (line in lines) {
        val n = line.trim().toIntOrNull()
        if (n == null) {
            bad++
        } else {
            sum += n
        }
    }
    println("$sum $bad")
    try {
        println("abc".toInt())
    } catch (e: NumberFormatException) {
        println("ошибка: ${e.message}")
    }
    println("12x".toIntOrNull() ?: -1)
}
```
```output
30 2
ошибка: For input string: "abc"
-1
```
`toIntOrNull()` возвращает число или `null`, поэтому неверные строки можно посчитать обычным `if`. Пробелы вокруг числа допустимы только после `trim()`: `" 20 ".toInt()` бросило бы исключение. `toInt()` на строке `abc` бросает `NumberFormatException`, которое перехватывает `try / catch`; оператор `?:` заменяет `null` значением по умолчанию.

<!-- section: precise -->
## Что нужно уметь

* читать строки до конца ввода: `while (true) { val line = readlnOrNull() ?: break ... }`;
* отличать `toInt()` (бросает исключение) от `toIntOrNull()` (возвращает `null`) и выбирать подходящий;
* использовать `trim()` перед разбором числа и понимать, что `7.5` и пустая строка - не целые числа;
* хранить результат разбора в `Int?` и проверять его через `== null` или `?:`;
* считать неверные и допустимые строки отдельными счётчиками, не прерывая программу;
* перехватывать `NumberFormatException` через `try / catch` и читать текст исключения.

<!-- section: mistakes -->
## На чём чаще всего ошибаются

* `readln()` на конце ввода бросает `ReadAfterEOFException`; для чтения до конца нужен `readlnOrNull()`.
* `toInt()` на строке с пробелами или точкой бросает `NumberFormatException`.
* Забытая проверка диапазона: число разобрано, но не входит в допустимые границы.
* Использование `!!` вместо проверки: на неверной строке программа падает.
* Широкий `catch (e: Exception)` скрывает ошибки, не связанные с разбором.

<!-- section: summary -->
## Итог

Если задания показались трудными, начните с уроков из списка: [Ввод данных: readln](lesson:kotlin.from-zero.reading-input), [Преобразование типов](lesson:kotlin.from-zero.type-conversion), [Значения, которых может не быть](lesson:kotlin.from-zero.nullable-values), [Исключения](lesson:kotlin.from-zero.exceptions). После повторения вернитесь к проверке: результат не зависит от числа попыток, а засчитанное остаётся засчитанным.
