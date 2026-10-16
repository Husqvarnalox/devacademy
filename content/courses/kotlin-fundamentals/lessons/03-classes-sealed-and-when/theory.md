<!-- section: simple -->
## Классы, data-классы, sealed-иерархии и when

В Kotlin класс объявляется короче, чем в Java: поля, конструктор и геттеры задаются одной строкой.

* **Первичный конструктор** стоит в заголовке класса: `class User(val name: String, var age: Int)`. Параметры с `val`/`var` сразу становятся свойствами.
* **`data class`** автоматически получает `equals`, `hashCode`, `toString`, `copy` и `componentN` для деструктуризации. Подходит для значений, которые описывают данные.
* **`sealed interface` / `sealed class`** задаёт закрытый набор вариантов. Все наследники известны компилятору, поэтому `when` по такому типу можно проверить на полноту: забытый вариант - ошибка компиляции, а не баг в выполнении.
* **`object`** - класс с единственным экземпляром (синглтон); **`companion object`** - место для «статических» членов и фабрик внутри класса.
* **`when`** - выражение, которое заменяет `switch` и цепочки `if`. Оно умеет сопоставлять значения, диапазоны, типы (`is`) и условия.

Вместе sealed-тип и `when` дают алгебраические типы данных: набор вариантов с данными и проверка полноты обработки.

<!-- section: precise -->
## Подробнее

**Свойства.** Свойство - это поле, геттер и (для `var`) сеттер. Можно объявить собственный геттер без поля: `val area get() = w * h`. Блок `init { ... }` выполняется при создании в порядке объявления вместе с инициализаторами свойств. Вторичные конструкторы (`constructor(...)`) должны делегировать первичному.

**Видимость и наследование.** Классы и члены по умолчанию `public` и **`final`**: чтобы наследоваться, класс нужно пометить `open`, а переопределяемый метод - `open`, при этом в наследнике он пишется с `override`. Видимости: `public`, `internal` (в пределах модуля), `protected`, `private`.

**Data class.** Деструктуризация `val (a, b) = point` опирается на `component1()`, `component2()`. В `equals`, `hashCode`, `toString` участвуют только свойства из **первичного** конструктора. `copy(x = 5)` создаёт изменённую копию, не меняя исходный объект; копия поверхностная.

**Sealed.** Прямые наследники sealed-типа должны находиться в том же пакете и модуле. Для вариантов без данных используют `object` или `data object` (у последнего читаемый `toString`). `when` по sealed-типу как **выражение** обязан быть исчерпывающим; ветку `else` лучше не писать, чтобы при добавлении варианта компилятор подсказал, что обработку надо дополнить.

**Object и companion.** `object Registry { ... }` создаётся лениво при первом обращении и потокобезопасно. У `companion object` нет «статического» уровня в языке: члены вызываются как `Class.member`, но на JVM это обычный объект-компаньон; как сделать настоящие статические методы, показано в [уроке про Java-взаимодействие](lesson:kotlin.fundamentals.java-interop). Фабричные функции в companion вместе с `private constructor` позволяют контролировать создание.

**When.** Ветки проверяются сверху вниз, срабатывает первая подходящая. Формы условий: значение (`1, 2 ->`), диапазон (`in 1..9 ->`), тип (`is String ->`), произвольное условие (`when { x > 0 -> ... }` без аргумента). Как выражение `when` обязан иметь `else` или быть исчерпывающим по sealed/enum/Boolean.

**Enum.** `enum class` подходит для набора констант без разных данных у вариантов; если варианты несут разные поля, берите sealed.

<!-- section: examples -->
## Примеры

**Пример 1. Свойства, `init` и data class.**

```kotlin run
class Rect(val w: Int, val h: Int) {
    val area get() = w * h
    init { require(w > 0 && h > 0) { "размеры должны быть положительными" } }
}

data class Point(val x: Int, val y: Int)

fun main() {
    println(Rect(3, 4).area)
    val p = Point(1, 2)
    val q = p.copy(y = 5)
    val (x, y) = q
    println("$p $q $x $y")
    println(p == Point(1, 2))
    println(setOf(p, Point(1, 2), q).size)
    println(Rect(2, 2) == Rect(2, 2))     // обычный класс: равенство по ссылке
}
```
```output
12
Point(x=1, y=2) Point(x=1, y=5) 1 5
true
2
false
```

**Пример 2. Sealed-иерархия и исчерпывающий `when`.**

```kotlin run
sealed interface Shape {
    data class Circle(val r: Int) : Shape
    data class Square(val side: Int) : Shape
    data object Empty : Shape
}

fun describe(s: Shape): String = when (s) {
    is Shape.Circle -> "круг r=${s.r}"
    is Shape.Square -> "квадрат ${s.side}x${s.side}"
    Shape.Empty -> "пусто"
}

fun main() {
    listOf(Shape.Circle(2), Shape.Square(3), Shape.Empty).forEach { println(describe(it)) }
    println(Shape.Empty)
    println(Shape.Circle(7))
}
```
```output
круг r=2
квадрат 3x3
пусто
Empty
Circle(r=7)
```

**Пример 3. `object`, `companion object` и разные формы `when`.**

```kotlin run
object Counter {
    private var n = 0
    fun next() = ++n
}

class Token private constructor(val text: String) {
    companion object {
        const val MAX = 4
        fun of(text: String): Token? = if (text.length <= MAX) Token(text) else null
    }
}

fun grade(score: Int): String = when {
    score >= 90 -> "A"
    score in 70..89 -> "B"
    else -> "C"
}

fun kind(x: Any): String = when (x) {
    1, 2, 3 -> "малое"
    is String -> "строка"
    in 10..20 -> "десятки"
    else -> "другое"
}

fun main() {
    println(Counter.next())
    println(Counter.next())
    println(Token.of("abc")?.text)
    println(Token.of("toolong"))
    println(Token.MAX)
    println(listOf(95, 75, 10).map(::grade))
    println(listOf(2, "x", 15, 99).map(::kind))
}
```
```output
1
2
abc
null
4
[A, B, C]
[малое, строка, десятки, другое]
```

<!-- section: mistakes -->
## Типичные ошибки

* **Ждать, что класс можно наследовать.** По умолчанию классы `final`; нужен `open`.
* **Использовать data class с изменяемыми `var` в ключах `HashMap`.** Хеш зависит от свойств, и после изменения запись «теряется».
* **Ждать, что `equals` data class учитывает свойства вне первичного конструктора.** Он их игнорирует.
* **Добавлять `else` в `when` по sealed-типу.** Компилятор перестаёт подсказывать, что новый вариант забыли обработать.
* **Путать `when` как оператор и как выражение.** Как выражение он обязан быть исчерпывающим.
* **Помещать наследников sealed в другой модуль.** Это запрещено: набор вариантов закрыт.
* **Считать `companion object` статическим.** Это настоящий объект; для статических методов на уровне JVM нужны аннотации.
* **Писать `copy` и ждать глубокой копии.** Вложенные изменяемые объекты общие у оригинала и копии.

<!-- section: summary -->
## Итоги

* Первичный конструктор, свойства и `init` заменяют поля, геттеры и конструкторы.
* `data class` даёт значение-подобное поведение: `equals`, `hashCode`, `toString`, `copy`, деструктуризацию.
* `sealed` + `when` без `else` - закрытый набор вариантов с проверкой полноты.
* `object` - синглтон, `companion object` - фабрики и константы класса.
* `when` объединяет значения, диапазоны, типы и условия.

Сравните с [записями и структурами C#](lesson:csharp.fundamentals.classes-records-structs) и [enum с данными в Rust](lesson:rust.fundamentals.structs-enums-pattern-matching). Дальше: [коллекции и последовательности](lesson:kotlin.fundamentals.collections-and-sequences).

<!-- section: facts -->
## Факты

* Прямые наследники sealed-класса или интерфейса должны быть в одном пакете и модуле.
* `data object` позволяет получить читаемый `toString` у объекта-варианта.
