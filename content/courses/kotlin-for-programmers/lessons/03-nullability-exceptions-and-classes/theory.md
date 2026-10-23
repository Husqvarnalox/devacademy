<!-- section: simple -->
## Зачем этот урок

В Kotlin отсутствие значения отражено в типе: `String` не может быть `null`, а `String?` может, и компилятор не даёт перепутать. Исключения непроверяемые (объявлять `throws` не нужно), `try` - выражение, а проверки аргументов оформляются как `require` и `check`. Класс пишется в одну строку, `new` нет, а `data class` сам даёт `equals`, `toString` и `copy`. Успешное прохождение (упражнение и квиз) засчитывает уроки 18, 22 и 23 курса «Kotlin с нуля».

## Главная мысль

Kotlin переносит ошибки из времени работы в время компиляции там, где это возможно: `NullPointerException` из `String?` не возникнет, пока вы не напишете `!!` или не получите значение из Java-кода. В остальном ловушки те же, что и у вас: исключения нужно ловить на правильном уровне, а равенство объектов зависит от того, определён ли `equals`.

<!-- section: examples -->
## Пример 1. Null-безопасность

```kotlin run
fun lengthOrMinusOne(s: String?): Int = s?.length ?: -1

fun main() {
    val name: String? = null
    val other: String? = "kotlin"
    println(name?.length)
    println(lengthOrMinusOne(name))
    println(lengthOrMinusOne(other))
    println(other?.uppercase()?.take(3))
    println("12".toIntOrNull() ?: 0)
    println("1x".toIntOrNull() ?: 0)
    if (other != null) {
        println(other.length)
    }
    println(name ?: "аноним")
    println(name.orEmpty().length)
    try {
        println(name!!.length)
    } catch (e: NullPointerException) {
        println("NPE")
    }
    val home = System.getenv("NO_SUCH_VARIABLE")
    println(home == null)
    val safe: String? = System.getenv("NO_SUCH_VARIABLE")
    println(safe?.length)
    try {
        val bad: String = System.getenv("NO_SUCH_VARIABLE")
        println(bad)
    } catch (e: NullPointerException) {
        println(e.message)
    }
}
```
```output
null
-1
6
KOT
12
0
6
аноним
0
NPE
true
null
getenv(...) must not be null
```

Разбор:

* `name?.length` - безопасный вызов: если слева `null`, результат `null`, а не исключение. `?:` (оператор Элвиса) подставляет значение по умолчанию; цепочка `s?.length ?: -1` заменяет проверку на `null`.
* После `if (other != null)` компилятор знает, что `other` не `null`, и разрешает `other.length` без `?.` (умное приведение, smart cast). Оно работает только для значений, которые не могут измениться между проверкой и использованием: для `val`, но не для изменяемых свойств класса.
* `name!!` означает «я уверен, что не `null`» и превращает ошибку типа в `NullPointerException` на этой строке. Это точка, где Kotlin возвращает вам Java-проблему; обычно `!!` заменяют на `?:` или явную проверку.
* **Платформенные типы.** Метод Java вроде `System.getenv` не говорит, может ли он вернуть `null`. Kotlin выводит для него «платформенный» тип `String!`, и компилятор доверяет вам: `home` можно использовать как `String` и как `String?`. Безопасно объявлять тип явно: `val safe: String? = ...`. Если объявить `String`, а вернулся `null`, исключение будет сразу на присваивании (`getenv(...) must not be null`), а не где-то дальше.
* `toIntOrNull()` вместо `toInt()` с перехватом - обычный способ разобрать пользовательский ввод без исключения.

## Пример 2. Исключения

```kotlin run
fun parse(s: String): Int = try {
    s.toInt()
} catch (e: NumberFormatException) {
    -1
}

fun checkAge(age: Int): Int {
    if (age < 0) {
        throw IllegalArgumentException("возраст отрицательный: $age")
    }
    return age
}

fun main() {
    println(parse("42"))
    println(parse("4x"))
    try {
        checkAge(-5)
    } catch (e: IllegalArgumentException) {
        println("ошибка: ${e.message}")
    } finally {
        println("finally")
    }
    val zero = 0
    val value = try {
        10 / zero
    } catch (e: ArithmeticException) {
        0
    }
    println(value)
    try {
        require(1 > 2) { "условие нарушено" }
    } catch (e: IllegalArgumentException) {
        println(e.message)
    }
    try {
        check(false) { "плохое состояние" }
    } catch (e: IllegalStateException) {
        println(e.message)
    }
    val result = runCatching { "abc".toInt() }
    println(result.isFailure)
    println(result.exceptionOrNull()?.message)
}
```
```output
42
-1
ошибка: возраст отрицательный: -5
finally
0
условие нарушено
плохое состояние
true
For input string: "abc"
```

Разбор:

* Исключения в Kotlin **непроверяемые**: сигнатура не объявляет `throws`, компилятор не требует `try/catch`. Поэтому ответственность за обработку лежит на вас, как в Python, C# и JavaScript, а не как в Java.
* `try` - выражение: значение последнего выражения блока (или `catch`) становится значением всего `try`. `finally` выполняется всегда и значения не меняет.
* `throw` - тоже выражение. Для проверки аргументов есть `require(условие) { сообщение }` (бросает `IllegalArgumentException`) и для состояния объекта `check(условие) { ... }` (бросает `IllegalStateException`).
* `runCatching { ... }` превращает исключение в значение `Result`; `exceptionOrNull()` и `getOrElse` позволяют разобрать его без `try`.
* Для ввода пользователя `toIntOrNull()` проще и быстрее, чем исключение: исключения предназначены для исключительных ситуаций.

## Пример 3. Классы и data class

```kotlin run
class Counter(val name: String) {
    var count = 0

    fun inc() {
        count++
    }
}

class Plain(val x: Int)

data class Point(val x: Int, val y: Int)

data class Box(val items: MutableList<Int>)

data class User(val id: Int) {
    var note = ""
}

fun main() {
    val c = Counter("hits")
    c.inc()
    c.inc()
    println("${c.name}=${c.count}")
    println(Plain(1) == Plain(1))
    val p = Point(1, 2)
    println(p)
    println(p == Point(1, 2))
    println(p === Point(1, 2))
    val q = p.copy(y = 5)
    println("$p $q")
    val (x, y) = q
    println(x + y)
    println(setOf(Point(1, 2), Point(1, 2), Point(2, 1)).size)
    val a = Box(mutableListOf(1))
    val b = a.copy()
    b.items.add(2)
    println(a.items)
    val u1 = User(1)
    u1.note = "важный"
    println("${u1 == User(1)} ${u1.note}")
}
```
```output
hits=2
false
Point(x=1, y=2)
true
false
Point(x=1, y=2) Point(x=1, y=5)
6
2
[1, 2]
true важный
```

Разбор:

* `class Counter(val name: String)` - это класс, конструктор и свойство `name` в одной строке; объект создают без `new`: `Counter("hits")`. Свойства вызываются без скобок (`c.count`), и `val` у свойства значит «только чтение».
* У обычного класса `==` вызывает `equals`, который по умолчанию сравнивает **ссылки**, поэтому `Plain(1) == Plain(1)` ложно. У `data class` `equals`, `hashCode`, `toString` и `copy` генерируются по свойствам из **первичного конструктора**: `Point(1, 2) == Point(1, 2)`, множество `setOf` склеивает дубликаты, `println(p)` печатает `Point(x=1, y=2)`.
* `===` сравнивает ссылки, и у `data class` он ложен для двух разных объектов с одинаковым содержимым.
* `copy(y = 5)` создаёт новый объект, меняя только указанные свойства. Копия **поверхностная**: `a.copy()` делит с `a` один и тот же список `items`, поэтому `b.items.add(2)` виден в `a`.
* Свойство, объявленное в теле класса (`note`), в `equals`, `hashCode` и `toString` не участвует: два `User(1)` равны, хотя `note` у них разный.
* Классы в Kotlin по умолчанию `final`: наследовать можно только то, что помечено `open`. Деструктуризация `val (x, y) = q` работает для data class.

**Попробуйте.** Добавьте в `Point` свойство `val label: String` в теле класса и сравните две точки с разными `label`; замените `val` в `data class Point(val x: Int, ...)` на `var` и проверьте, что `p == Point(1, 2)` меняется после `p.x = 9`.

<!-- section: precise -->
* `T` - не `null`, `T?` - может быть `null`. Присвоить `null` в `T` нельзя, вызвать метод у `T?` напрямую - тоже.
* Операторы: `?.` (безопасный вызов), `?:` (значение по умолчанию), `!!` (утверждение: бросает `NullPointerException`), `as?` (приведение, дающее `null` при неудаче). Функции: `orEmpty()`, `toIntOrNull()`, `toDoubleOrNull()`, `readlnOrNull()`, `firstOrNull()`, `getOrNull()`.
* Smart cast действует после проверки `!= null` и `is` для локальных `val`, параметров и `var`, которые не изменяются между проверкой и использованием. Для свойств класса с `var` и открытых свойств он не работает.
* Платформенный тип `T!` появляется у значений из Java; компилятор не проверяет его на `null` до использования.
* Исключения: иерархия `Throwable` → `Exception` / `Error`; все исключения Kotlin непроверяемые. `try`, `if`, `when` и `throw` - выражения; тип `throw` - `Nothing`.
* `require` → `IllegalArgumentException`, `check` → `IllegalStateException`, `error("...")` → `IllegalStateException`. `use { }` закрывает ресурс.
* `NumberFormatException` бросают `toInt()` и `toDouble()` на неверной строке; пробелы вокруг числа не допускаются. Целое деление на ноль - `ArithmeticException`.
* `class Name(val a: Int, var b: String)` - первичный конструктор и свойства; дополнительная инициализация - в блоке `init { }`. Методы объявляют через `fun` внутри класса, текущий объект - `this`.
* `data class` требует хотя бы одного параметра первичного конструктора с `val` или `var`; генерирует `equals`, `hashCode`, `toString`, `copy` и `componentN` (для деструктуризации).
* `object` создаёт единственный экземпляр; `==` на значениях в `List`/`Set`/`Map` использует `equals` элементов.

<!-- section: mistakes -->
**1. `null` в обычный тип.**

```text
Main.kt:2:18: error: null cannot be a value of a non-null type 'Int'.
```

**2. Вызов метода у nullable-значения.**

```text
Main.kt:3:14: error: only safe (?.) or non-null asserted (!!.) calls are allowed on a nullable receiver of type 'String?'.
```

Решение - `s?.length`, проверка `if (s != null)` или `s.orEmpty()`; `s!!.length` тоже скомпилируется, но превращает ошибку в падение:

```text
Exception in thread "main" java.lang.NullPointerException
```

**3. Нечисловая строка.**

```text
Exception in thread "main" java.lang.NumberFormatException: For input string: "abc"
```

Пробел тоже ломает разбор: `" 12".toInt()` бросает то же исключение, поэтому ввод сначала обрезают `trim()`.

**4. Деление целых на ноль.**

```text
Exception in thread "main" java.lang.ArithmeticException: / by zero
```

**5. Присвоение `val`-свойству.** Для обычного и data-класса:

```text
Main.kt:5:7: error: 'val' cannot be reassigned.
```

**6. `new`.** В Kotlin такого слова нет:

```text
Main.kt:4:13: error: unresolved reference 'new'.
```

**7. Наследование от final-класса.**

```text
Main.kt:2:11: error: this type is final, so it cannot be extended.
```

Нужен модификатор `open` у родителя.

**8. Значение из Java как `String`.** `val s: String = System.getenv("X")` даёт `NullPointerException: getenv(...) must not be null` на присваивании. Тип `String?` решает проблему.

**9. Молчаливые ошибки.** Сравнение объектов обычных классов через `==` (всегда ложно, пока не переопределён `equals`); поверхностная копия `copy()` с изменяемым свойством; `catch (e: Exception)` слишком широкий; `runCatching`, который перехватывает и ошибки программиста; свойства в теле data class, не участвующие в `equals`.

<!-- section: summary -->
Теперь вы знаете, как в Kotlin устроены `T?` и `T`, `?.`, `?:` и `!!`, чем опасны платформенные типы, как работают непроверяемые исключения, `require` и `check`, и чем `data class` отличается от обычного класса. Следующий шаг - вводный курс про компиляцию и запуск: [Как запускается Kotlin](lesson:kotlin.first-steps.how-kotlin-runs). Для подробностей по темам: [Значения, которых может не быть](lesson:kotlin.from-zero.nullable-values), [Исключения](lesson:kotlin.from-zero.exceptions), [Первый класс](lesson:kotlin.from-zero.first-class).

<!-- section: programmer -->
## Главное для тех, кто уже программирует

* Тип `T?` отделяет значения, которые могут быть `null` (в Java и C# без включённых аннотаций компилятор молчит, в Python и JavaScript проверок нет). `?.` и `?:` заменяют цепочки `if`.
* `!!` - явное «утверждаю, что не `null`»: это `NullPointerException`, только записанный открыто. В Rust похожий `unwrap()`, в Swift - `!`.
* Платформенные типы: значения из Java-кода не имеют признака `null`-допустимости; объявляйте тип явно.
* Исключения непроверяемые, как в C#, Python и JavaScript; в Java нужен `throws`. `try`, `if`, `when` - выражения.
* `require` / `check` / `error` вместо ручных `if (...) throw`. `Result` и `runCatching` - функциональный стиль, но они ловят всё подряд.
* Классы по умолчанию `final`; `new` нет; конструктор и поля описаны в заголовке класса; свойства вместо геттеров и сеттеров.
* `data class` - аналог `record` в Java и C# (но можно изменять через `var`), `@dataclass` в Python, `case class` в Scala.
* `==` вызывает `equals`; без data class и без переопределения он сравнивает ссылки, как `==` в Java. `===` - сравнение ссылок.
* `copy()` поверхностный; вложенные изменяемые коллекции делят данные.
* Нет `static`: функции верхнего уровня и `object` / `companion object`.

<!-- section: deep-dive -->
## Откуда берётся `NullPointerException` в Kotlin

Тип `T?` существует только на этапе компиляции: в байткоде нет разницы между `String` и `String?`. Чтобы не потерять безопасность на границе с Java, компилятор вставляет проверки (`Intrinsics.checkNotNullExpressionValue`) при передаче значения платформенного типа в `String`, поэтому исключение появляется на самом присваивании, а не позже. Внутри самого Kotlin-кода `NullPointerException` остаётся возможным в трёх местах: `!!`, значения из Java и неинициализированные свойства (`lateinit`). Для `Int?` на JVM используется обёртка `Integer`: `Int` - примитив `int`, а `Int?` - объект, поэтому `===` для двух `Int?` с равными значениями может дать `false`, и компилятор предупреждает о таком сравнении.
