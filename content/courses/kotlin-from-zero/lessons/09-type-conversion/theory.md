<!-- section: simple -->
## Зачем превращать один тип в другой

Из прошлого урока вы знаете: `readln()` всегда возвращает текст. Если на вход пришли строки `20` и `22`, программа получила два текста, а не два числа. Склеить их можно, а сложить - нет: для компьютера `"20"` - это два символа, а число `20` - совсем другое значение.

Похожая задача возникает и внутри программы: надо поделить `10` на `4` и получить `2.5`, а не `2`, или вставить число в текст как отдельное значение. Везде, где нужно перейти от одного вида значения к другому, используют **преобразование типов**.

## Одна новая идея: Kotlin ничего не преобразует сам, преобразование пишут явно

Для каждого целевого типа есть своя команда, которая вызывается через точку после значения:

| Команда | Что делает | Пример | Результат |
|---|---|---|---|
| `toInt()` | в целое число | `"42".toInt()` | `42` |
| `toDouble()` | в число с дробной частью | `"12.5".toDouble()` | `12.5` |
| `toLong()` | в большое целое | `"7".toLong()` | `7` |
| `toString()` | в текст | `42.toString()` | `"42"` |

Это работает и для текста, и для чисел: `toDouble()` можно вызвать у `Int`, `toInt()` - у `Double`. Привычная цепочка для ввода выглядит так:

```kotlin
val count = readln().toInt()
```

Сначала `readln()` читает строку, затем `toInt()` превращает её в число. Результат - переменная типа `Int`, с которой можно считать.

<!-- section: examples -->
## Пример 1. Текст и число - разные значения

```kotlin run
fun main() {
    val text = "42"
    val number = text.toInt()
    println(number + 1)
    println(text + 1)
}
```
```output
43
421
```

Разбор: `number` - число, и `number + 1` складывает: `43`. А `text` - текст, поэтому `text + 1` склеивает: к символам `42` добавляется символ `1`, получается `421`. Одинаково выглядящие значения ведут себя по-разному.

## Пример 2. Цена и количество

```kotlin run
fun main() {
    val price = "12.5".toDouble()
    val quantity = "4".toInt()
    println(price * quantity)
    println("Итого: ${price * quantity}")
}
```
```output
50.0
Итого: 50.0
```

Разбор: `12.5` стало `Double`, `4` - `Int`. Произведение `Double` на `Int` - это `Double`, поэтому ответ `50.0`, с дробной частью. Так же результат попадает в шаблон. В настоящей программе тексты `"12.5"` и `"4"` пришли бы из `readln()`; здесь они записаны прямо в коде, чтобы пример можно было запустить.

## Пример 3. Деление, усечение и ещё несколько преобразований

```kotlin run
fun main() {
    val total = 10
    val people = 4
    println(total / people)
    println(total.toDouble() / people)
    println(3.9.toInt())
    println((-3.9).toInt())
    println(5.toString() + "7")
    println(2.toLong() * 3000000000)
}
```
```output
2
2.5
3
-3
57
6000000000
```

Разбор по строкам:

* `total / people` - оба `Int`, поэтому деление целочисленное: `2`.
* `total.toDouble() / people` - слева теперь `Double` (`10.0`), и деление дробное: `2.5`. Это ответ на вопрос из урока об арифметике.
* `3.9.toInt()` не округляет, а **отбрасывает** дробную часть: `3`. Для отрицательных чисел отбрасывание идёт в сторону нуля: `-3`.
* `5.toString() + "7"` превращает `5` в текст `"5"`, и `+` склеивает два текста: `57`.
* `2.toLong() * 3000000000` - число типа `Long`, поэтому результат помещается: `6000000000`.

**Попробуйте.** Замените в примере 3 `total.toDouble() / people` на `total / people.toDouble()` и предскажите результат. Затем замените `"4".toInt()` в примере 2 на `"4.0".toInt()` и прочитайте сообщение об ошибке.

<!-- section: precise -->
## Точные правила

* `s.toInt()`, `s.toLong()`, `s.toDouble()` разбирают текст `s`. Допустимы знак в начале (`-5`, `+5`) и, для `Double`, дробная часть через **точку** (`3.5`). Для `Int` дробная часть не допустима: `"4.0".toInt()` - ошибка.
* Если текст не подходит, во время работы возникает `NumberFormatException` с сообщением `For input string: "..."`. В кавычках показан весь текст, который не удалось разобрать, - так видно лишние пробелы.
* Пробелы вокруг числа не отбрасываются: `"12 ".toInt()` - ошибка. Пустая строка тоже не число: `"".toInt()` - ошибка.
* `x.toDouble()`, `x.toLong()`, `x.toInt()` у чисел превращают число в число другого типа. Для `Double -> Int` дробная часть отбрасывается в сторону нуля (`3.9` -> `3`, `-3.9` -> `-3`), а не округляется.
* `Int -> Double` и `Int -> Long` не теряют значение. А слишком большое `Long -> Int` теряет, поэтому такое преобразование нужно писать осознанно.
* `x.toString()` превращает любое значение в текст. Число в шаблоне `"$x"` превращается в текст автоматически, отдельный вызов не нужен.
* Результат команды - **новое** значение. Сама переменная не меняет тип: `val text = "42"` остаётся текстом, а `text.toInt()` - отдельное число, которое нужно сохранить или сразу использовать.
* В выражении `Int / Int` дробная часть пропадает ещё до преобразования. Чтобы получить дробный результат, преобразуйте один из операндов **до** деления: `a.toDouble() / b`, а не `(a / b).toDouble()`.
* Команды вызывают со скобками: `toInt()`, не `toInt`.

<!-- section: mistakes -->
## Типичные ошибки

**1. Текст, который не является числом.**

```kotlin
fun main() {
    val n = "abc".toInt()
    println(n)
}
```

Программа собирается, но при запуске печатает:

```text
Exception in thread "main" java.lang.NumberFormatException: For input string: "abc"
	at java.base/java.lang.NumberFormatException.forInputString(NumberFormatException.java:67)
	at java.base/java.lang.Integer.parseInt(Integer.java:588)
	at java.base/java.lang.Integer.parseInt(Integer.java:685)
	at MainKt.main(Main.kt:2)
```

(В полном сообщении есть ещё строка со стеком.) `NumberFormatException` означает «неверный формат числа». В кавычках показан текст, который не получилось разобрать. Строка стека `Main.kt:2` показывает, какая строка программы вызвала `toInt()`.

**2. Запятая в дробном числе.**

```kotlin
fun main() {
    val price = "3,5".toDouble()
    println(price)
}
```

```text
Exception in thread "main" java.lang.NumberFormatException: For input string: "3,5"
	at java.base/jdk.internal.math.FloatingDecimal.readJavaFormatString(FloatingDecimal.java:2054)
	at java.base/jdk.internal.math.FloatingDecimal.parseDouble(FloatingDecimal.java:110)
	at java.base/java.lang.Double.parseDouble(Double.java:938)
	at MainKt.main(Main.kt:2)
```

Дробную часть `toDouble()` ждёт после точки. В русской записи принята запятая, но программа её не понимает: пишите `3.5`.

**3. Лишний пробел или пустая строка.**

```kotlin
fun main() {
    val n = readln().toInt()
    println(n + 1)
}
```

Если на входе `12 ` (с пробелом на конце), программа остановится:

```text
Exception in thread "main" java.lang.NumberFormatException: For input string: "12 "
	at java.base/java.lang.NumberFormatException.forInputString(NumberFormatException.java:67)
	at java.base/java.lang.Integer.parseInt(Integer.java:588)
	at java.base/java.lang.Integer.parseInt(Integer.java:685)
	at MainKt.main(Main.kt:2)
```

Если ввод - пустая строка, сообщение будет `For input string: ""`. Кавычки в сообщении помогают заметить пробел там, где его не видно глазами. Очищать пробелы вы научитесь в одном из следующих уроков, а проверять ввод до преобразования - в уроке об ошибках и вводе.

**4. Забыть скобки.**

```kotlin
fun main() {
    val n = readln().toInt
    println(n)
}
```

```text
Main.kt:2:22: error: function invocation 'toInt()' expected.
    val n = readln().toInt
                     ^^^^^
```

`toInt` без скобок - не вызов. Добавьте `()`.

**5. Ожидать, что Kotlin преобразует число сам.**

```kotlin
fun main() {
    val count = 5
    val ratio: Double = count
    println(ratio)
}
```

```text
Main.kt:3:23: error: initializer type mismatch: expected 'Double', actual 'Int'.
    val ratio: Double = count
                      ^
```

Переменная типа `Double` не принимает `Int` без явного преобразования. Напишите `count.toDouble()`.

**6. Умножение текстов.**

```kotlin
fun main() {
    val a = "20"
    val b = "22"
    println(a * b)
}
```

```text
Main.kt:4:15: error: unresolved reference 'times' for operator '*' on receiver of type 'String'.
    println(a * b)
              ^
```

У текста нет умножения. Сначала превратите значения в числа: `a.toInt() * b.toInt()`.

<!-- section: summary -->
## Итоги

* `readln()` возвращает текст; число из него получают командами `toInt()`, `toDouble()`, `toLong()`.
* Между числами тоже нужны явные преобразования: `x.toDouble()`, `x.toInt()`. Kotlin сам их не делает.
* `Double.toInt()` отбрасывает дробную часть. Чтобы деление было дробным, один из операндов превращают в `Double` до деления.
* Неподходящий текст (`abc`, `3,5`, `12 `) вызывает `NumberFormatException` с сообщением `For input string: "..."`.

В следующем уроке ([Форматированный вывод](lesson:kotlin.from-zero.formatted-output)) вы научитесь печатать дробные числа с нужным числом знаков после точки.

<!-- section: programmer -->
## Для тех, кто уже программирует

* Нет неявных преобразований между числовыми типами, даже расширяющих (`Int -> Long`, `Int -> Double`): в Java и C# они допустимы. Преобразование всегда пишут явно через `toXxx()`.
* `toInt()` у `String` на JVM - это `Integer.parseInt`, поэтому формат и сообщение `NumberFormatException` такие же, как в Java. Культурно-независимый разбор: десятичный разделитель - точка.
* Пробелы не отбрасываются автоматически, в отличие от `int("12 ")` в Python или `Number(" 12 ")` в JavaScript. Если нужно, вызывают `trim()`.
* Безопасные версии - `toIntOrNull()` и `toDoubleOrNull()`, возвращающие `null` вместо исключения. Они разобраны в уроке 18, когда появятся типы, допускающие `null`.
* `Double.toInt()` усекает к нулю и насыщается на границах (`1e20.toInt()` равно `Int.MAX_VALUE`, `NaN.toInt()` равно `0`), как приведение в Kotlin/JVM; в Java приведение `(int) 1e20` ведёт себя так же. Для округления есть `roundToInt()` из `kotlin.math`.
* `toString()` определён для любого значения, а в шаблоне `"$x"` вызывается автоматически. `Char.toInt()` устарел: для кода символа используют `'a'.code`, а для цифры - `'7'.digitToInt()`.
