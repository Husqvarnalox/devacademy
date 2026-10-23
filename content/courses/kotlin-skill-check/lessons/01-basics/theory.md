<!-- section: simple -->
## Что проверяет эта проверка

Переменные `val` и `var`, типы `Int`, `Double`, `Boolean`, `Char`, `String`, арифметика и остаток от деления, целочисленное деление, чтение ввода через `readln()`, преобразование строк в числа, форматированный вывод, сравнение и логические операторы, `if / else` и `when`, циклы `while` и `for`, `break` и `continue`, чтение сообщений компилятора.

## Что даёт прохождение

Проверка состоит из короткого упражнения с запуском тестов и квиза на чтение кода (порог квиза - 75%). Если пройдены обе части, засчитываются уроки: [Первая программа](lesson:kotlin.from-zero.first-program), [Как читать сообщения об ошибках](lesson:kotlin.from-zero.compiler-errors), [Вывод текста](lesson:kotlin.from-zero.output), [Переменные: val и var](lesson:kotlin.from-zero.variables), [Основные типы: Double, Boolean, Char, Long](lesson:kotlin.from-zero.basic-types), [Арифметика](lesson:kotlin.from-zero.arithmetic), [Строки: длина, символы, регистр](lesson:kotlin.from-zero.strings), [Ввод данных: readln](lesson:kotlin.from-zero.reading-input), [Преобразование типов](lesson:kotlin.from-zero.type-conversion), [Форматированный вывод](lesson:kotlin.from-zero.formatted-output), [Сравнение и логика](lesson:kotlin.from-zero.comparison-and-logic), [Условия: if и else](lesson:kotlin.from-zero.if-else), [Выбор из многих вариантов: when](lesson:kotlin.from-zero.when), [Цикл while](lesson:kotlin.from-zero.while-loop), [Цикл for и диапазоны](lesson:kotlin.from-zero.for-loop), [Управление циклом: break и continue](lesson:kotlin.from-zero.loop-control).

Если не пройдены, ничего не отнимается: прогресс сохраняется, а система покажет первый незавершённый урок из этого списка как место для повторения. Проверку можно пройти снова в любой момент, и открыть её можно без каких-либо условий.

<!-- section: examples -->
## Пример того, что понадобится

```kotlin run
fun main() {
    var sum = 0
    var odd = 0
    for (x in 1..10) {
        if (x % 3 == 0) {
            continue
        }
        sum += x
        if (x % 2 != 0) {
            odd++
        }
    }
    println("$sum $odd ${sum / 4} ${sum / 4.0}")
    for (i in 1..6) {
        if (i % 3 == 0) {
            continue
        }
        println("$i " + if (i % 2 == 0) "чётное" else "нечётное")
    }
}
```
```output
37 3 9 9.25
1 нечётное
2 чётное
4 чётное
5 нечётное
```
Цикл `for (x in 1..10)` перебирает числа, `continue` пропускает кратные трём, `%` определяет чётность. Деление целых даёт целое (`37 / 4` отбрасывает дробную часть), а `sum / 4.0` - дробное. `if` в последней строке работает как выражение и подставляет слово в строку.

<!-- section: precise -->
## Что нужно уметь

* читать число из строки ввода через `readln().toInt()` и повторять чтение нужное число раз в цикле `for`;
* начинать максимум с первого прочитанного числа, а не с нуля, чтобы он был верным и для отрицательных чисел;
* использовать `%` и сравнение с нулём (`x % 2 == 0`) для проверки чётности, в том числе для отрицательных чисел;
* помнить, что деление двух `Int` целочисленное, и выбирать `Double`, когда нужна дробь;
* выбирать тип по размеру значений и замечать переполнение `Int`;
* предсказывать работу `break` и `continue` во вложенных условиях и циклах;
* читать сообщения `kotlinc` и находить строку с ошибкой.

<!-- section: mistakes -->
## На чём чаще всего ошибаются

* Проверка нечётности через `x % 2 == 1`: для `-3` остаток равен `-1`, и условие не срабатывает.
* Максимум, начатый с `0`, неверен для набора из одних отрицательных чисел.
* `sum / count` с целыми `sum` и `count` отбрасывает дробную часть; при `count == 0` деление на ноль бросает исключение.
* Граница цикла на единицу: `1 until n` вместо `1..n`.
* `1_000_000 * 3000` вычисляется в `Int` и переполняется, даже если результат присваивается `Long`.

<!-- section: summary -->
## Итог

Если задания показались трудными, начните с уроков из списка: [Первая программа](lesson:kotlin.from-zero.first-program), [Как читать сообщения об ошибках](lesson:kotlin.from-zero.compiler-errors), [Вывод текста](lesson:kotlin.from-zero.output), [Переменные: val и var](lesson:kotlin.from-zero.variables), [Основные типы: Double, Boolean, Char, Long](lesson:kotlin.from-zero.basic-types), [Арифметика](lesson:kotlin.from-zero.arithmetic), [Строки: длина, символы, регистр](lesson:kotlin.from-zero.strings), [Ввод данных: readln](lesson:kotlin.from-zero.reading-input), [Преобразование типов](lesson:kotlin.from-zero.type-conversion), [Форматированный вывод](lesson:kotlin.from-zero.formatted-output), [Сравнение и логика](lesson:kotlin.from-zero.comparison-and-logic), [Условия: if и else](lesson:kotlin.from-zero.if-else), [Выбор из многих вариантов: when](lesson:kotlin.from-zero.when), [Цикл while](lesson:kotlin.from-zero.while-loop), [Цикл for и диапазоны](lesson:kotlin.from-zero.for-loop), [Управление циклом: break и continue](lesson:kotlin.from-zero.loop-control). После повторения вернитесь к проверке: результат не зависит от числа попыток, а засчитанное остаётся засчитанным.
