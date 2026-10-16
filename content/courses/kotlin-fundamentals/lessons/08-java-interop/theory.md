<!-- section: simple -->
## Взаимодействие с Java: платформенные типы и аннотации

Kotlin компилируется в тот же байткод, что и Java, и свободно вызывает Java-библиотеки. Но у границы двух языков есть правила, и их нужно знать.

* **Платформенные типы.** Java не различает `String` и `String?`, поэтому значение из Java имеет «платформенный» тип `String!`: компилятор не требует проверки на `null`. Вы сами решаете, считать ли его `String` или `String?`. Если выбрать `String`, а Java вернёт `null`, исключение случится в месте присваивания.
* **Аннотации nullability.** Если Java-код помечен `@Nullable`/`@NotNull` (JetBrains, JSpecify и другие), Kotlin видит точные типы `String?` и `String`.
* **Kotlin глазами Java.** Файл-функции становятся статическими методами класса `ИмяФайлаKt`, `companion object` - вложенным классом `Companion`, а свойство - полем с геттером и сеттером. Аннотации `@JvmStatic`, `@JvmOverloads`, `@JvmField`, `@JvmName`, `@Throws` настраивают то, как Java видит ваш код.
* **SAM-преобразование.** Java-интерфейсы с одним методом (`Runnable`, `Comparator`) передают как лямбды.

<!-- section: precise -->
## Подробнее

**Платформенные типы.** Тип `T!` означает «`T` или `T?`»; его нельзя написать в коде, он только выводится. Для безопасности явно объявляйте тип: `val s: String? = javaCall()`. Типичные источники: `System.getProperty`, `HashMap.get`, поля и методы Java-классов без аннотаций.

**Сопоставление коллекций.** Kotlin-типы `List`, `MutableList`, `Map` соответствуют Java-интерфейсам `java.util.List`, `java.util.Map`. Одному Java-типу отвечают read-only и изменяемый варианты: Kotlin не видит разницы в Java, поэтому передавая `List<T>` в Java, помните, что Java может его изменить.

**Проверки на границе.** Для публичных функций Kotlin вставляет проверки: если Java передаст `null` в параметр типа `String`, будет `NullPointerException` в самом начале метода, а не где-то дальше.

**`@JvmOverloads`.** Для функции с параметрами по умолчанию Java видит только полную сигнатуру. `@JvmOverloads` генерирует перегрузки, убирая параметры по умолчанию справа налево: для `f(a, b = 1, c = 2)` появятся `f(a)`, `f(a, b)` и `f(a, b, c)`.

**`@JvmStatic`.** Член `object` или `companion object` по умолчанию вызывается из Java как `Foo.Companion.bar()`. С `@JvmStatic` в содержащем классе появляется настоящий static-метод `Foo.bar()`.

**`@JvmField` и `const`.** Свойство обычно доступно через геттер. `@JvmField` делает его публичным полем без геттера и сеттера; `const val` превращается в `public static final` константу. `lateinit var` также открывает поле.

**`@JvmName`.** Переименовывает метод или геттер в байткоде (`@get:JvmName("title")`), а `@file:JvmName("Utils")` меняет имя класса, который получает функции верхнего уровня файла. Полезно при конфликте сигнатур после стирания типов.

**`@Throws`.** В Kotlin нет checked exceptions, но Java-код не сможет написать `catch (IOException e)` для метода без `throws`. `@Throws(IOException::class)` добавляет объявление в сигнатуру метода.

**Nullability-аннотации.** Kotlin распознаёт несколько наборов (в документации перечислены JetBrains, JSpecify, Android, JSR-305 и другие). Для JSpecify строгий уровень сообщений включён по умолчанию; для остальных набор и уровень настраиваются флагами компилятора. Подключаются аннотации зависимостью Java-кода. В среде DevAcademy такие зависимости не подключены, поэтому примеры ниже с `@Nullable` приведены как Java-код без запуска.

<!-- section: examples -->
## Примеры

**Пример 1. Платформенные типы, SAM и коллекции.** Источник платформенного типа - JDK без аннотаций.

```kotlin run
fun main() {
    val p = System.getProperty("devacademy.absent")     // тип String!
    println(p == null)

    val safe: String? = System.getProperty("devacademy.absent")
    println(safe ?: "нет")

    try {
        val t: String = System.getProperty("devacademy.absent")   // проверка в месте присваивания
        println(t.length)
    } catch (e: NullPointerException) {
        println("NPE")
    }

    val m = java.util.HashMap<String, Int>()
    println(m.get("k"))                                  // Int! -> null

    val r = Runnable { println("SAM: Runnable") }
    r.run()
    val byLength = java.util.Comparator<String> { a, b -> a.length - b.length }
    println(listOf("ccc", "a", "bb").sortedWith(byLength))
    println(java.util.ArrayList<Int>().also { it.add(1) }::class.java.name)
}
```
```output
true
нет
NPE
null
SAM: Runnable
[a, bb, ccc]
java.util.ArrayList
```

**Пример 2. Что Java видит у Kotlin-класса.** Состав класса проверяется рефлексией; методы отсортированы, потому что порядок в `declaredMethods` не определён.

```kotlin run
import java.lang.reflect.Modifier

class Greeter(val name: String) {
    @JvmOverloads
    fun greet(greeting: String = "Hello", punct: Char = '!'): String = "$greeting, $name$punct"

    companion object {
        @JvmField
        val DEFAULT = Greeter("world")
        const val LIMIT = 3

        @JvmStatic
        fun create(name: String) = Greeter(name)

        fun helper() = 1
    }
}

fun main() {
    val c = Greeter::class.java
    println(c.declaredMethods.filter { it.name == "greet" }.map { it.parameterCount }.sorted())
    println(c.declaredMethods.filter { Modifier.isStatic(it.modifiers) && !it.isSynthetic }.map { it.name })
    println(c.declaredFields.filter { Modifier.isStatic(it.modifiers) }.map { it.name }.sorted())
    println(Greeter.create("kotlin").greet())
    println(Greeter.DEFAULT.greet("Hi", '?'))
    println(c.declaredMethods.any { it.name == "helper" })                // helper остался в Companion
    println(c.declaredClasses.map { it.simpleName })
}
```
```output
[0, 1, 2]
[create]
[Companion, DEFAULT, LIMIT]
Hello, kotlin!
Hi, world?
false
[Companion]
```

**Пример 3. `@Throws`, `@JvmName` и проверка `null` на границе.**

```kotlin run
import java.io.IOException
import java.lang.reflect.InvocationTargetException

class Box(@get:JvmName("title") val name: String) {
    @Throws(IOException::class)
    fun load(): String = name

    fun shout(s: String) = s.uppercase()
}

fun main() {
    val c = Box::class.java
    println(c.getMethod("load").exceptionTypes.map { it.simpleName })
    println(c.methods.map { it.name }.filter { it == "title" || it == "getName" })
    try {
        c.getMethod("shout", String::class.java).invoke(Box("x"), null)   // так вызовет Java-код с null
    } catch (e: InvocationTargetException) {
        println(e.cause!!::class.simpleName)
    }
}
```
```output
[IOException]
[title]
NullPointerException
```

**Пример 4 (Java, не запускается). Аннотации nullability.** Библиотека `org.jetbrains.annotations` не подключена к среде запуска, поэтому код приведён как справочный.

```java
import org.jetbrains.annotations.NotNull;
import org.jetbrains.annotations.Nullable;

public class Users {
    public @Nullable String find(@NotNull String id) { return null; }  // Kotlin увидит String? и параметр String
    public String legacy() { return null; }                           // без аннотаций: String! (платформенный)
}
```

```kotlin
val user: String? = Users().find("42")   // безопасно: тип String?
val old = Users().legacy()               // String!, проверять должны вы
```

**Пример 5 (Java, не запускается). Вызов Kotlin из Java.**

```java
Greeter g = Greeter.create("kotlin");       // @JvmStatic
String s = g.greet("Hi");                   // @JvmOverloads
Greeter d = Greeter.DEFAULT;                // @JvmField
int limit = Greeter.LIMIT;                  // const val
int h = Greeter.Companion.helper();         // без @JvmStatic
```

<!-- section: mistakes -->
## Типичные ошибки

* **Присваивать платформенное значение типу без `?`.** `NullPointerException` случится в вашем коде, хотя причина в Java.
* **Не аннотировать собственный Java-код.** Kotlin увидит `String!` и перестанет вам помогать.
* **Забыть `@JvmOverloads`.** Java не увидит укороченных сигнатур и должна передавать все аргументы.
* **Ждать статических методов от `companion object`.** Без `@JvmStatic` вызов из Java идёт через `Foo.Companion`.
* **Думать, что `@Throws` меняет поведение.** Он влияет только на сигнатуру в байткоде.
* **Передавать `List<T>` в Java как неизменяемый.** Java может изменить его через `java.util.List`.
* **Вызывать Kotlin-функции с `null` из Java и ждать результата.** Для параметров типа `String` проверка бросит исключение.
* **Рассчитывать, что порядок `getDeclaredMethods` стабилен.** Спецификация его не гарантирует: сортируйте.

<!-- section: summary -->
## Итоги

* Значения из Java без аннотаций имеют платформенный тип `T!`: проверка `null` лежит на вас, лучше явно писать `T?`.
* `@Nullable`/`@NotNull` в Java-коде превращаются в обычные nullable/ненулевые типы Kotlin.
* `@JvmOverloads`, `@JvmStatic`, `@JvmField`, `@JvmName`, `@Throws` задают, как ваш Kotlin-код выглядит в Java.
* Лямбды заменяют Java-интерфейсы с одним методом (SAM).
* Публичные функции Kotlin защищены проверками параметров на `null`.

Сравните с [JVM-моделью Java](lesson:java.first-steps.how-java-runs) и [равенством объектов в Java](lesson:java.first-steps.primitives-objects-and-equality). Вернитесь к [null-безопасности](lesson:kotlin.fundamentals.types-and-null-safety), чтобы связать правила с границей между языками.

<!-- section: facts -->
## Факты

* Тип из Java без аннотаций получает платформенный вид `T!`, для которого компилятор Kotlin не требует проверок `null`.
* `@JvmOverloads` генерирует перегрузки, последовательно убирая параметры по умолчанию справа.
