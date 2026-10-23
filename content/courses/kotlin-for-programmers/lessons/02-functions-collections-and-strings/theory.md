<!-- section: simple -->
## Зачем этот урок

Функции, списки, словари и строки есть в любом языке, поэтому в Kotlin их легко писать «по привычке» - и именно там прячутся отличия. У функций есть аргументы по умолчанию и именованные аргументы, а параметры нельзя переприсвоить. `val` не делает список неизменяемым, а тип `List` не гарантирует, что список никто не изменит. `map[key]` возвращает не значение, а значение **или** `null`. Строки неизменяемы, `==` сравнивает их содержимое, а `split` оставляет пустые элементы. Успешное прохождение (упражнение и квиз) засчитывает уроки 17, 19, 20 и 21 курса «Kotlin с нуля».

## Главная мысль

В Kotlin коллекции разделены на **интерфейс только для чтения** (`List`, `Map`, `Set`) и **изменяемый** (`MutableList`, `MutableMap`, `MutableSet`). Это разделение живёт в типах, а не в самих объектах: `val` защищает только ссылку, а `List` лишь не показывает методы изменения. Когда вы принимаете или возвращаете коллекцию, думайте о том, кто ещё держит на неё ссылку и в каком виде.

<!-- section: examples -->
## Пример 1. Функции

```kotlin run
fun area(w: Int, h: Int): Int = w * h

fun greet(name: String, greeting: String = "Привет", punctuation: String = "!"): String =
    "$greeting, $name$punctuation"

fun log(message: String) {
    println("log: $message")
}

fun divide(a: Int, b: Int): Pair<Int, Int> = Pair(a / b, a % b)

fun addTotal(xs: MutableList<Int>) {
    xs.add(xs.sum())
}

fun main() {
    println(area(3, 4))
    println(greet("Анна"))
    println(greet("Борис", punctuation = "?"))
    println(greet(greeting = "Здравствуйте", name = "Вера"))
    val r = log("готово")
    println(r)
    val (q, rem) = divide(17, 5)
    println("$q $rem")
    fun square(x: Int) = x * x
    println(square(7))
    val nums = mutableListOf(1, 2, 3)
    addTotal(nums)
    println(nums)
}
```
```output
12
Привет, Анна!
Привет, Борис?
Здравствуйте, Вера!
log: готово
kotlin.Unit
3 2
49
[1, 2, 3, 6]
```

Разбор:

* Тело из одного выражения записывают через `=` и тип результата выводится сам, но у публичных функций его принято указывать. Если результата нет, функция возвращает `Unit` (это настоящий объект, его можно напечатать: `kotlin.Unit`), а не `void`.
* Аргументы по умолчанию и **именованные аргументы** заменяют перегрузки и «построители»: `greet("Борис", punctuation = "?")` задаёт только то, что отличается. Именованные аргументы можно переставлять.
* Параметры функции - это `val`: `x = x + 1` внутри функции не скомпилируется. Но если параметр - изменяемая коллекция, функция получает **ту же** коллекцию, а не копию: `addTotal(nums)` изменил список вызывающего кода.
* Несколько результатов возвращают через `Pair`, `Triple` или `data class` и разбирают деструктуризацией: `val (q, rem) = ...`.
* Функции можно объявлять внутри функций (`square`), и функция верхнего уровня не требует класса.

## Пример 2. Списки и словари

```kotlin run
fun main() {
    val fixed = listOf(3, 1, 2)
    val items = mutableListOf(3, 1, 2)
    val view: List<Int> = items
    items.add(0)
    println("$fixed $items $view")
    println(items.sorted())
    println(items)
    items.sort()
    println(items)
    val copy = items.toList()
    items.clear()
    println("${copy.size} ${items.size}")
    println(fixed == listOf(3, 1, 2))
    println(fixed.getOrNull(5))
    try {
        println(fixed[5])
    } catch (e: IndexOutOfBoundsException) {
        println("нет элемента")
    }

    val stock = mutableMapOf("tea" to 3, "jam" to 0)
    stock["milk"] = 5
    stock["tea"] = stock.getValue("tea") + 1
    println(stock)
    println("${stock["tea"]} ${stock["cake"]} ${stock["cake"] ?: -1} ${stock.getOrDefault("cake", 9)}")
    for ((name, n) in stock) {
        if (n == 0) {
            println("$name закончился")
        }
    }
    println(stock.containsKey("jam"))
    println(stock.keys)
}
```
```output
[3, 1, 2] [3, 1, 2, 0] [3, 1, 2, 0]
[0, 1, 2, 3]
[3, 1, 2, 0]
[0, 1, 2, 3]
4 0
true
null
нет элемента
{tea=4, jam=0, milk=5}
4 null -1 9
jam закончился
true
[tea, jam, milk]
```

Разбор:

* `items` объявлен через `val`, но `add`, `sort` и `clear` работают: `val` запрещает только `items = ...`. А у `listOf(...)` метода `add` нет вовсе, поэтому его вызов - ошибка компиляции.
* `view` - не копия, а второе имя для того же списка с урезанным набором методов: после `items.add(0)` в нём тоже четыре элемента. Копию делает `toList()`.
* `sorted()` возвращает **новый** отсортированный список, `sort()` сортирует изменяемый список на месте.
* `fixed == listOf(3, 1, 2)` истинно: `==` для коллекций сравнивает содержимое, а не ссылки.
* `fixed[5]` бросает исключение (его тип - подкласс `IndexOutOfBoundsException`), `getOrNull(5)` возвращает `null`.
* `stock["cake"]` возвращает `Int?`, а не `Int`: ключа может не быть. Дальше его обрабатывают через `?:`, `getOrDefault` или явную проверку; `getValue` бросает исключение, если ключа нет.
* `mapOf` и `mutableMapOf` сохраняют **порядок вставки** (в Java `HashMap` порядка не гарантирует). Обход пар - `for ((key, value) in map)`.

## Пример 3. Строки

```kotlin run
fun main() {
    val line = "  Hello, Kotlin!  "
    val s = line.trim()
    println("[$s] ${s.length}")
    println(s.substring(7, 13))
    println(s.substring(7))
    println("${s.indexOf("o")} ${s.indexOf("z")} ${s.contains("Kot")} ${s.startsWith("Hello")}")
    println(s.replace("l", "L"))
    println("a,b,,c".split(","))
    println("a  b".split(" "))
    println("a  b".split(" ").filter { it.isNotEmpty() })
    println(listOf("x", "y", "z").joinToString("-"))
    println("${s[0]}${s[1]} ${s[s.length - 1]}")
    println(s.uppercase() + " " + s.lowercase())
    val a = "kot"
    val b = a + "lin"
    println("${b == "kotlin"} ${b.equals("KOTLIN", ignoreCase = true)} ${"abc" < "abd"}")
    println("%s: %d символов".format("kotlin", 6))
    println("ab".repeat(3))
    println(
        """
        Строка
          в несколько строк
        """.trimIndent()
    )
}
```
```output
[Hello, Kotlin!] 14
Kotlin
Kotlin!
4 -1 true true
HeLLo, KotLin!
[a, b, , c]
[a, , b]
[a, b]
x-y-z
He !
HELLO, KOTLIN! hello, kotlin!
true true true
kotlin: 6 символов
ababab
Строка
  в несколько строк
```

Разбор:

* Строки неизменяемы: `trim`, `replace`, `uppercase` возвращают **новую** строку, а присвоить символ (`s[0] = 'x'`) нельзя. Символ строки `s[i]` - это `Char`.
* `substring(a, b)` берёт символы от `a` включительно до `b` **не** включая; индексы начинаются с нуля. Выход за границы бросает `StringIndexOutOfBoundsException`.
* `indexOf` возвращает `-1`, если подстроки нет; в отличие от `map[key]` здесь нет `null`.
* `split` возвращает `List<String>` и **не** удаляет пустые элементы: `"a  b".split(" ")` даёт три элемента, средний пустой. Пустые части убирают отдельной фильтрацией.
* `==` сравнивает содержимое, `<` и `>` - лексикографически (через `compareTo`), без учёта регистра - `equals(..., ignoreCase = true)`.
* Многострочная строка в `"""` вместе с `trimIndent()` убирает общий отступ.

**Попробуйте.** Замените `items.sorted()` на `items.sort()` (что напечатает `println(items.sort())`?), `fixed[5]` на `fixed[2]`, `"a  b".split(" ")` на `"a  b".split("  ")`; предскажите результат до запуска.

<!-- section: precise -->
* Функция: `fun имя(параметр: Тип, ...): Результат { ... }` или `= выражение`. Параметры неизменяемы, могут иметь значения по умолчанию; именованные аргументы допустимы в любом порядке после позиционных. Функция без результата возвращает `Unit`.
* Интерфейсы коллекций: `List<T>`, `Set<T>`, `Map<K, V>` - только чтение; `MutableList<T>`, `MutableSet<T>`, `MutableMap<K, V>` - с изменением. Создание: `listOf`, `mutableListOf`, `setOf`, `mapOf`, `mutableMapOf`, `emptyList()`.
* `val` - защищает ссылку, не содержимое. Чтобы получить копию, используют `toList()`, `toMutableList()`, `toMap()`.
* `list[i]` и `map[key]` - операторы доступа; для `Map` результат `V?`. `getOrNull`, `getOrElse`, `getOrDefault`, `getValue` отличаются тем, что делают при отсутствии элемента.
* `==` для `List`, `Set`, `Map` - структурное сравнение. `Set` и `Map` по умолчанию (`setOf`, `mapOf`) помнят порядок вставки (`LinkedHashSet`, `LinkedHashMap`).
* `sorted()`, `reversed()`, `distinct()` возвращают новую коллекцию; `sort()`, `reverse()`, `add`, `remove`, `clear` изменяют `MutableList` на месте.
* `String` неизменяема; `length`, `s[i]`, `substring(from, to)`, `indexOf`, `contains`, `startsWith`, `endsWith`, `replace`, `trim`, `split`, `lowercase()`, `uppercase()`, `repeat`, `isEmpty()`, `isBlank()`. Сравнение: `==`, `compareTo`, `equals(other, ignoreCase = true)`.
* `joinToString(separator)` собирает коллекцию в строку; `"%s %d".format(...)` форматирует (с `Locale.ROOT` для дробных чисел).
* Индексы строк и списков идут с нуля; последний элемент - `size - 1` или `last()`; диапазон индексов - `indices`.

<!-- section: mistakes -->
**1. Изменение списка только для чтения.** `listOf` не имеет `add`:

```text
Main.kt:3:8: error: unresolved reference 'add' on receiver of type 'List<Int>'.
```

Нужен `mutableListOf` или копия `toMutableList()`.

**2. Переприсваивание параметра или `val`.**

```text
Main.kt:2:5: error: 'val' cannot be reassigned.
```

**3. Присвоение символу строки.** `s[0] = 'x'` не компилируется:

```text
Main.kt:3:6: error: no 'set' operator method providing array access.
```

**4. Значение словаря как обычное число.** `val n: Int = stock["tea"]`:

```text
Main.kt:3:16: error: initializer type mismatch: expected 'Int', actual 'Int?'.
```

**5. Забытый `return`.** Если ветка не возвращает значение, а функция объявлена с типом:

```text
Main.kt:5:1: error: missing return statement.
```

**6. Выход за границы.** Эти ошибки компилятор не видит; они проявляются при запуске:

```text
Exception in thread "main" java.lang.ArrayIndexOutOfBoundsException: Index 3 out of bounds for length 3
Exception in thread "main" java.lang.StringIndexOutOfBoundsException: Range [2, 5) out of bounds for length 3
Exception in thread "main" java.util.NoSuchElementException: Key milk is missing in the map.
```

Точный текст и тип исключения при индексе зависят от вида списка и версии JDK; общий признак - `IndexOutOfBoundsException` в иерархии.

**7. Молчаливые ошибки.** Функция меняет переданный `MutableList`, а вызывающий код этого не ждёт; `List`, который в другом месте - тот же изменяемый список; `split(" ")` с двойными пробелами и пустые элементы в результате; сортировка через `sorted()` без использования результата (`xs.sorted()` ничего не меняет в `xs`).

<!-- section: summary -->
Теперь вы знаете, как в Kotlin устроены функции с аргументами по умолчанию, коллекции только для чтения и изменяемые, `Map` с результатом `V?` и неизменяемые строки с `split`, который оставляет пустые элементы. Следующий шаг - null-безопасность, исключения и первые классы: [Null-безопасность, исключения и классы: ловушки Kotlin](lesson:kotlin.programmers.nullability-exceptions-and-classes). Для подробностей по темам: [Функции](lesson:kotlin.from-zero.functions), [Списки](lesson:kotlin.from-zero.lists), [Словари](lesson:kotlin.from-zero.maps), [Методы строк](lesson:kotlin.from-zero.string-methods).

<!-- section: programmer -->
## Главное для тех, кто уже программирует

* Функции: `fun`, типы после имени, `Unit` вместо `void`, значения по умолчанию и именованные аргументы вместо перегрузок и builder. Параметры неизменяемы.
* `List` / `MutableList` - разные интерфейсы (в Java и C# `List` изменяем, в Python `list` тоже). `List<T>` - только чтение, но не гарантия неизменяемости: это представление.
* `val list = mutableListOf()` можно изменять; `val` лишь запрещает переприсвоение (как `final` в Java, но с более короткой записью).
* `map[key]` возвращает `V?` (в Python - `KeyError`, в Java - молчаливый `null`, в Go - нулевое значение). Компилятор заставляет решить, что делать с отсутствием.
* `mapOf` помнит порядок вставки, в отличие от `HashMap` в Java; для сортировки по ключам есть `toSortedMap()`.
* `==` на коллекциях и строках - структурное (как в Python и C#; в Java для этого нужен `equals`).
* Строки неизменяемы (как в Java, C#, Python, Go); `s[i]` даёт `Char`; `substring(a, b)` не включает `b` (как в Java); `split` оставляет пустые элементы.
* Деструктуризация `val (a, b) = pair` работает для `Pair`, `data class` и записей `Map`.
* Списки индексируются с нуля; `list[-1]` не работает (в Python работает) - используйте `last()`.

<!-- section: deep-dive -->
## Почему `List` в Kotlin - не неизменяемый

Интерфейсы `List`, `Set`, `Map` в Kotlin на JVM компилируются в обычные `java.util.List`, `Set`, `Map`; «только чтение» - это то, что видит компилятор, а не свойство объекта. Поэтому `listOf(...)` и приведение `view as MutableList<Int>` компилируются, а при запуске результат зависит от реального класса. Зато интерфейсы только для чтения ковариантны (`List<out E>`): `List<String>` можно передать туда, где ждут `List<Any>`, а для `MutableList` такая подстановка небезопасна. Настоящие неизменяемые коллекции дают сторонние библиотеки (`kotlinx.collections.immutable`).
