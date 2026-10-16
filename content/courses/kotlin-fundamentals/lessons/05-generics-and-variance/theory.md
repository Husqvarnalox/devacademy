<!-- section: simple -->
## Обобщения: параметры типов, вариантность и reified

Обобщённый код работает с типом-параметром: `class Box<T>`, `fun <T> first(list: List<T>): T`. Kotlin использует ту же схему, что и Java: на JVM параметры типов **стираются** при компиляции. Отсюда три темы урока.

* **Ограничения** (`<T : Comparable<T>>`) говорят, что умеет тип-параметр, и открывают его методы внутри функции.
* **Вариантность.** Можно ли передать `List<Int>` туда, где ждут `List<Number>`? Для `List` - да (`out`, ковариантность), для `MutableList` - нет. В Kotlin вариантность задаётся **на стороне объявления** словами `out` и `in`, а не символами `? extends` в каждом месте использования, как в Java.
* **`reified`.** Обычно во время выполнения нельзя проверить `x is T`: тип стёрт. В `inline`-функции с параметром `reified T` компилятор подставляет настоящий тип в место вызова, и проверка работает.

<!-- section: precise -->
## Подробнее

**Стирание типов.** Во время выполнения `List<String>` и `List<Int>` - один и тот же класс; `x is List<String>` нельзя проверить, допустимо только `x is List<*>`. Компилятор следит за типами до запуска и вставляет приведения сам.

**Ограничения.** `fun <T : Number> twice(x: T)` допускает только числа. Несколько ограничений задают через `where`: `fun <T> f(x: T) where T : Comparable<T>, T : CharSequence`. Верхняя граница по умолчанию `Any?`: без ограничения `T` может быть nullable, чтобы запретить `null`, пишут `T : Any`.

**Вариантность на стороне объявления.**
* `interface Producer<out T>`: `T` встречается только на выходе (возвращаемые значения). Тогда `Producer<String>` - подтип `Producer<Any>`.
* `interface Consumer<in T>`: `T` встречается только на входе (параметры). Тогда `Consumer<Any>` - подтип `Consumer<String>`: потребитель любых значений годится как потребитель строк.
* Без модификатора тип **инвариантен**: `MutableList<Int>` не является ни подтипом, ни надтипом `MutableList<Number>`. Иначе в список целых можно было бы положить `Double`.
* Компилятор проверяет использование: параметр `out T` нельзя принять в метод, параметр `in T` нельзя вернуть.

**Проекции в месте использования.** Если класс инвариантен, но вам нужна только чтение или только запись, пишите `MutableList<out Number>` (чтение) или `MutableList<in Int>` (запись). `Array<out Any>` в параметре функции допускает массив любого подтипа. Звёздочка `List<*>` означает «список чего-то»: читать можно как `Any?`.

**Reified.** Условия: функция `inline` и параметр помечен `reified`. Тогда доступны `is T`, `T::class`, `filterIsInstance<T>()`. Ограничения: нельзя вызвать такую функцию из Java с выбором типа, а `reified` нельзя использовать у классов (только у inline-функций и свойств).

**Дженерики и `null`.** `fun <T> id(x: T): T` принимает и `String?`, в этом случае `T` выводится как `String?`.

<!-- section: examples -->
## Примеры

**Пример 1. Обобщённый класс и ограничения.**

```kotlin run
class Stack<T> {
    private val items = ArrayList<T>()
    fun push(x: T) { items.add(x) }
    fun pop(): T? = if (items.isEmpty()) null else items.removeAt(items.lastIndex)
    val size get() = items.size
}

fun <T : Comparable<T>> maxOf3(a: T, b: T, c: T): T = maxOf(a, maxOf(b, c))

fun <T> describe(x: T) where T : Comparable<T>, T : CharSequence = "${x.length}:${x}"

fun main() {
    val s = Stack<String>()
    s.push("a"); s.push("b")
    println(s.pop())
    println(s.size)
    println(maxOf3(3, 9, 4))
    println(maxOf3("kiwi", "apple", "pear"))
    println(describe("abc"))
    println(listOf("x")::class == listOf(1)::class)   // стирание: классы совпадают
}
```
```output
b
1
9
pear
3:abc
true
```

**Пример 2. Вариантность: `out`, `in` и проекции.**

```kotlin run
interface Producer<out T> { fun get(): T }
interface Consumer<in T> { fun accept(x: T) }

class Strings(private val s: String) : Producer<String> { override fun get() = s }
class Printer : Consumer<Any> { override fun accept(x: Any) = println("печать: $x") }

fun readAll(p: Producer<Any>) = println(p.get())
fun feed(c: Consumer<String>) = c.accept("строка")

fun copyAll(from: MutableList<out Number>, to: MutableList<in Number>) {
    for (x in from) to.add(x)             // читаем из out, пишем в in
}

fun main() {
    readAll(Strings("привет"))             // Producer<String> годится как Producer<Any>
    feed(Printer())                        // Consumer<Any> годится как Consumer<String>

    val ints: List<Int> = listOf(1, 2)
    val numbers: List<Number> = ints       // List<out E>: ковариантность
    println(numbers)

    val a = mutableListOf(1, 2)
    val b = mutableListOf<Number>(0.5)
    copyAll(a, b)
    println(b)
    // val bad: MutableList<Number> = a   // ошибка компиляции: MutableList инвариантен

    val stars: List<*> = listOf("x", 1)
    println(stars.size)
    println(stars[0])
}
```
```output
привет
печать: строка
[1, 2]
[0.5, 1, 2]
2
x
```

**Пример 3. `reified` и стирание типов.**

```kotlin run
inline fun <reified T> List<Any?>.countOf(): Int = count { it is T }

inline fun <reified T> typeName(): String = T::class.simpleName ?: "?"

fun <T> brokenCheck(x: Any, @Suppress("UNUSED_PARAMETER") sample: T): Boolean =
    x is List<*>                          // x is List<T> не скомпилируется: T стёрт

fun main() {
    val data: List<Any?> = listOf(1, "a", 2.5, null, 3, "b")
    println(data.countOf<Int>())
    println(data.countOf<String>())
    println(data.countOf<Any>())          // null не считается Any
    println(typeName<Double>())
    println(typeName<List<String>>())
    println(data.filterIsInstance<String>())
    println(brokenCheck(listOf(1), "t"))
}
```
```output
2
2
5
Double
List
[a, b]
true
```

<!-- section: mistakes -->
## Типичные ошибки

* **Ждать, что `List<Int>` можно передать как `MutableList<Number>`.** Изменяемые коллекции инвариантны.
* **Пытаться проверить `is List<String>`.** Из-за стирания проверка невозможна; используйте `is List<*>` и проверку элементов.
* **Забыть `inline` у `reified`-функции.** Без него тип во время выполнения недоступен.
* **Перегружать функции, различающиеся только параметром типа.** `fun f(x: List<String>)` и `fun f(x: List<Int>)` после стирания совпадают (конфликт JVM-сигнатур).
* **Ставить `out` там, где `T` принимается в параметрах.** Компилятор запретит; обычно нужна проекция `in` или другой дизайн.
* **Считать `T` ненулевым.** Без `T : Any` в `T` может быть `String?`.
* **Использовать `Any` вместо ограничения.** Теряется проверка типов; ограничение `T : Comparable<T>` лучше.

<!-- section: summary -->
## Итоги

* Параметры типов стираются на JVM: во время выполнения `List<String>` и `List<Int>` неразличимы.
* `out` (производитель) даёт ковариантность, `in` (потребитель) - контравариантность; без модификатора тип инвариантен.
* Проекции `out`/`in` и `*` задают вариантность в месте использования.
* `inline` + `reified` возвращает информацию о типе во время выполнения для `is T` и `T::class`.
* Ограничения `T : Bound` и `where` определяют, что можно делать со значением типа `T`.

Сравните с [обобщениями C#](lesson:csharp.fundamentals.generics-and-collections) (там типы не стираются) и [трейтами и обобщениями Rust](lesson:rust.fundamentals.traits-and-generics). Дальше: [обработка ошибок](lesson:kotlin.fundamentals.error-handling).

<!-- section: facts -->
## Факты

* В отличие от Java, Kotlin не использует wildcard-типы; вариантность задаётся модификаторами `out` и `in`.
* `List<out E>` ковариантен, а `MutableList<E>` инвариантен.
