<!-- section: simple -->
## Зачем форматировать число

Дробные числа печатаются «как есть», и результат бывает неудобным. Сумма в чеке должна выглядеть как `37.50`, а не `37.5`. Для денег нужны ровно два знака после точки, для процентов - один, для средней оценки - два или три. А иногда число печатается с длинным «хвостом»: сумма `0.1 + 0.2` выводится как `0.30000000000000004`.

Эти хвосты не ошибка Kotlin: дробные числа хранятся приближённо (о причинах говорилось в уроке об арифметике). Но выводить их пользователю в таком виде нельзя. Управлять видом числа при печати помогает **форматирование** - описание того, как выглядит результат.

## Одна новая идея: формат - это текст с «дырками», в которые подставляют значения

```kotlin
"%.2f".format(Locale.ROOT, total)
```

Строка `"%.2f"` - это **шаблон формата**. Знак `%` начинает **спецификатор** - описание одного места, куда подставляется значение. Буква после точки с числом говорит, какого типа значение там ожидается:

| Спецификатор | Для какого значения | Пример шаблона | Результат при значении |
|---|---|---|---|
| `%d` | целое (`Int`, `Long`) | `"%d шт."` | `3` -> `3 шт.` |
| `%f` | дробное (`Double`) | `"%f"` | `2.5` -> `2.500000` |
| `%.2f` | `Double`, два знака после точки | `"%.2f"` | `2.5` -> `2.50` |
| `%s` | любое значение как текст | `"%s!"` | `Анна` -> `Анна!` |
| `%%` | сам знак процента | `"100%%"` | `100%` |

Значения для «дырок» перечисляют в скобках после `Locale.ROOT`: по порядку, слева направо.

### Зачем нужен Locale.ROOT

Если не указать локаль, Kotlin берёт региональные настройки компьютера. На компьютере с русскими настройками `"%.2f".format(2.5)` печатает `2,50` - с запятой, а на другом - `2.50` с точкой. Такая программа даёт разные ответы на разных машинах, и проверка вывода не пройдёт. Параметр `Locale.ROOT` («нейтральная локаль») фиксирует поведение: разделитель всегда точка. Чтобы он был доступен, в начале файла пишут строку `import java.util.Locale`. Во всех упражнениях этого курса используйте именно эту форму.

<!-- section: examples -->
## Пример 1. Два знака после точки

```kotlin run
import java.util.Locale

fun main() {
    val price = 2.5
    println(price)
    println("%.2f".format(Locale.ROOT, price))
    println("%.1f".format(Locale.ROOT, 2.25))
    println("%.0f".format(Locale.ROOT, 7.5))
    println(0.1 + 0.2)
    println("%.2f".format(Locale.ROOT, 0.1 + 0.2))
}
```
```output
2.5
2.50
2.3
8
0.30000000000000004
0.30
```

Разбор по строкам:

* `println(price)` печатает число как есть: `2.5`. А `%.2f` добавляет недостающий нуль: `2.50`.
* `%.1f` с числом `2.25` даёт `2.3`: лишние цифры не отбрасываются, а **округляются** (половина округляется вверх).
* `%.0f` - ноль знаков после точки: `7.5` превращается в `8`.
* Последние две строки показывают хвост от приближённого хранения и то, как формат его убирает.

## Пример 2. Несколько значений и выравнивание

```kotlin run
import java.util.Locale

fun main() {
    val name = "Кофе"
    val qty = 3
    val price = 149.9
    println("%s x%d".format(Locale.ROOT, name, qty))
    println("%s: %.2f".format(Locale.ROOT, name, price * qty))
    println("%5d|%-5d|%05d".format(Locale.ROOT, 42, 42, 42))
    println("%8.2f|".format(Locale.ROOT, 3.14159))
    println("%-8s|%8s|".format(Locale.ROOT, "кот", "кот"))
    println("Скидка %d%%".format(Locale.ROOT, 15))
    println("%,d".format(Locale.ROOT, 1234567))
}
```
```output
Кофе x3
Кофе: 449.70
   42|42   |00042
    3.14|
кот     |     кот|
Скидка 15%
1,234,567
```

Разбор:

* В первой строке два спецификатора и два значения: первое значение идёт в первый спецификатор, второе - во второй.
* `%5d` задаёт **ширину** 5: число дополняется пробелами слева. `%-5d` с минусом выравнивает по левому краю. `%05d` дополняет нулями.
* `%8.2f` - ширина 8 и два знака после точки. Столбики из таких строк получаются ровными.
* `%-8s` и `%8s` выравнивают текст по левому и по правому краю.
* `%%` печатает знак процента.
* `%,d` разделяет тысячи запятыми (при `Locale.ROOT` - именно запятыми).

## Пример 3. Формат как обычная строка

```kotlin run
import java.util.Locale

fun main() {
    val a = 2
    val b = 3
    val line = "%d / %d = %.3f".format(Locale.ROOT, a, b, a.toDouble() / b)
    println(line)
    println(line.length)
}
```
```output
2 / 3 = 0.667
13
```

Разбор: результат `format` - обычная строка. Её можно сохранить в переменную, измерить (`length`), вставить в шаблон. Спецификаторов три - значений тоже три, и порядок совпадает: `a`, `b`, результат деления. Деление дробное, потому что слева стоит `a.toDouble()`.

**Попробуйте.** В примере 1 уберите `Locale.ROOT` вместе со следующей за ним запятой и посмотрите на вывод у себя. Затем верните его. Измените `%.2f` на `%.4f` и предскажите вывод.

<!-- section: precise -->
## Точные правила

* Вызов `"шаблон".format(Locale.ROOT, значение1, значение2, ...)` возвращает новую строку. Исходный шаблон не меняется.
* Нужен импорт `import java.util.Locale` в начале файла, до `fun main()`.
* Спецификатор имеет вид `%[флаги][ширина][.точность]тип`:
  * тип `d` - целое, `f` - дробное, `s` - текст (подходит для любого значения);
  * ширина - минимальное число символов; короткое значение дополняется пробелами слева, при флаге `-` - справа;
  * флаг `0` дополняет нулями (для чисел); флаг `,` ставит разделитель тысяч;
  * точность у `f` - число знаков после точки, по умолчанию 6 (`"%f"` для `2.5` даёт `2.500000`).
* Число спецификаторов должно совпадать с числом значений, а типы - подходить: `%d` принимает только целые, `%f` - только `Double`. Проверка происходит при **запуске**, не при компиляции.
* `%f` не принимает `Int`: `"%.2f".format(Locale.ROOT, 3)` бросает исключение. Целое сначала преобразуйте: `3.toDouble()`.
* При форматировании `%.Nf` число округляется (половина - вверх), а не отбрасывается; в отличие от `toInt()`.
* Сам знак процента в шаблоне пишут как `%%`. Обратная косая черта для него не нужна.
* Форматирование только меняет вид строки. Значение в переменной остаётся прежним: `price` равно `2.5`, пока вы не присвоите ему другое.
* Без `Locale.ROOT` разделитель дробной части и тысяч зависит от настроек компьютера (например, запятая вместо точки). Это самая частая причина того, что программа даёт разный вывод у разных людей.

<!-- section: mistakes -->
## Типичные ошибки

**1. `%d` для дробного числа.**

```kotlin
import java.util.Locale

fun main() {
    val price = 2.5
    println("%d".format(Locale.ROOT, price))
}
```

Программа собирается, а при запуске печатает:

```text
Exception in thread "main" java.util.IllegalFormatConversionException: d != java.lang.Double
	at java.base/java.util.Formatter$FormatSpecifier.failConversion(Formatter.java:4534)
	at java.base/java.util.Formatter$FormatSpecifier.printInteger(Formatter.java:3072)
	at java.base/java.util.Formatter$FormatSpecifier.print(Formatter.java:3027)
	at java.base/java.util.Formatter.format(Formatter.java:2797)
```

(В полном сообщении строк стека больше.) Сообщение `d != java.lang.Double` читается так: спецификатор `d` не подходит к значению типа `Double`. Для дробных используйте `%f` или `%.2f`.

**2. `%f` для целого числа.**

```kotlin
import java.util.Locale

fun main() {
    println("%.2f".format(Locale.ROOT, 3))
}
```

```text
Exception in thread "main" java.util.IllegalFormatConversionException: f != java.lang.Integer
	at java.base/java.util.Formatter$FormatSpecifier.failConversion(Formatter.java:4534)
	at java.base/java.util.Formatter$FormatSpecifier.printFloat(Formatter.java:3085)
	at java.base/java.util.Formatter$FormatSpecifier.print(Formatter.java:3033)
	at java.base/java.util.Formatter.format(Formatter.java:2797)
```

`f != java.lang.Integer`: целое число не подходит к `%f`. Напишите `3.toDouble()` или используйте `%d`.

**3. Спецификаторов больше, чем значений.**

```kotlin
import java.util.Locale

fun main() {
    println("%s: %s".format(Locale.ROOT, "Кофе"))
}
```

```text
Exception in thread "main" java.util.MissingFormatArgumentException: Format specifier '%s'
	at java.base/java.util.Formatter.format(Formatter.java:2796)
	at java.base/java.util.Formatter.format(Formatter.java:2734)
	at java.base/java.lang.String.format(String.java:4473)
	at MainKt.main(Main.kt:4)
```

`MissingFormatArgumentException` - «не хватает значения». Для второго `%s` значения нет. Сверьте число спецификаторов с числом значений.

**4. Забытый импорт.**

```kotlin
fun main() {
    println("%.2f".format(Locale.ROOT, 2.5))
}
```

```text
Main.kt:2:27: error: unresolved reference 'Locale'.
    println("%.2f".format(Locale.ROOT, 2.5))
                          ^^^^^^
```

Компилятор не знает имени `Locale`, пока оно не подключено. Добавьте в первую строку файла `import java.util.Locale`.

**5. Неизвестная буква после `%`.**

```kotlin
import java.util.Locale

fun main() {
    println("%q".format(Locale.ROOT, 3))
}
```

```text
Exception in thread "main" java.util.UnknownFormatConversionException: Conversion = 'q'
	at java.base/java.util.Formatter$FormatSpecifier.conversion(Formatter.java:2964)
	at java.base/java.util.Formatter$FormatSpecifier.<init>(Formatter.java:3000)
	at java.base/java.util.Formatter.parse(Formatter.java:2855)
	at java.base/java.util.Formatter.format(Formatter.java:2780)
```

Такой буквы в формате нет. Заодно это значит, что одиночный знак `%` в шаблоне всегда начинает спецификатор: чтобы напечатать процент, пишите `%%`.

<!-- section: summary -->
## Итоги

* `"%.2f".format(Locale.ROOT, x)` возвращает строку с `x` и двумя знаками после точки; число знаков задаёт цифра после точки.
* `%d` - для целых, `%f` - для `Double`, `%s` - для текста, `%%` - сам знак процента. Ширина (`%8.2f`), флаги `-`, `0`, `,` управляют выравниванием.
* `Locale.ROOT` и `import java.util.Locale` делают вывод одинаковым на любом компьютере.
* Число и типы значений проверяются при запуске: несоответствие даёт `IllegalFormatConversionException` или `MissingFormatArgumentException`.

В следующем уроке ([Сравнение и логика](lesson:kotlin.from-zero.comparison-and-logic)) вы научитесь получать ответ «да» или «нет» на вопросы вроде «больше ли сумма ста?».

<!-- section: programmer -->
## Для тех, кто уже программирует

* `String.format(locale, ...)` в Kotlin - это обёртка над `java.lang.String.format`, спецификаторы те же, что в Java (`%d`, `%s`, `%.2f`, `%n`...). Строки в стиле Python (`f"{x:.2f}"`) или шаблоны JavaScript здесь не работают: внутри `"${...}"` форматирование не задают.
* Локаль по умолчанию берётся из JVM. `Locale.ROOT` - нейтральный выбор для данных, которые читает машина или проверяет тест; для вывода человеку используют `Locale.getDefault()` или явную локаль. `println(x)` для `Double` и `toDouble()` от локали не зависят, а `format` без локали - зависит.
* Если число нужно округлить как значение (а не только показать), используют `kotlin.math.round`, `roundToInt()`; для денег чаще работают в целых копейках или `BigDecimal`.
* Форматирование через `%f` использует округление `HALF_UP` по десятичному представлению числа, а не по точному двоичному значению: `"%.2f".format(Locale.ROOT, 0.125)` печатает `0.13`.
* Существует и `println("%.2f".format(x))` без локали - он работает, но даёт запятую на русской системе. В учебных задачах с проверкой вывода такой вариант ненадёжен.
* Есть `buildString`, `padStart`, `padEnd` для ручного выравнивания; позже вы увидите их в уроках о методах строк.
