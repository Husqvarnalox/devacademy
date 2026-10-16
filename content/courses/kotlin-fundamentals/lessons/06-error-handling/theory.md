<!-- section: simple -->
## Обработка ошибок: исключения, require/check и Result

В Kotlin нет проверяемых исключений: ни одно исключение не нужно объявлять в сигнатуре или оборачивать в `try`. Это даёт короткий код, но ответственность за то, какие ошибки возможны, лежит на вас. Для этого язык и стандартная библиотека предлагают набор приёмов.

* **`try`/`catch`/`finally` - выражения.** Блок `try` возвращает значение: `val n = try { s.toInt() } catch (e: NumberFormatException) { 0 }`.
* **`require` и `check`** проверяют условия: `require` - аргументы (бросает `IllegalArgumentException`), `check` - состояние объекта (бросает `IllegalStateException`). `error("текст")` бросает `IllegalStateException` сразу.
* **`Result<T>` и `runCatching`** превращают исключение в значение, которое можно обработать цепочкой `map`, `recover`, `getOrElse`, `fold`.
* **Ожидаемые отсутствия - не исключения.** Для «может не быть» используйте nullable (`toIntOrNull`), для «несколько исходов» - sealed-типы; исключения оставьте для нарушений ожиданий.

<!-- section: precise -->
## Подробнее

**Иерархия.** Все исключения - наследники `Throwable`; `Exception` для ошибок программы, `Error` - для критических сбоев JVM (`OutOfMemoryError`, `StackOverflowError`), которые обычно не ловят. Часто встречаются `IllegalArgumentException`, `IllegalStateException`, `NumberFormatException` (наследник `IllegalArgumentException`), `ArithmeticException` (целочисленное деление на ноль), `IndexOutOfBoundsException`, `NoSuchElementException`, `NullPointerException`, `ClassCastException`.

**`try` как выражение.** Значение блока `try` или выбранного `catch` становится значением всего выражения; `finally` значение не меняет, но выполняется всегда (в том числе после `return` в `try`). `throw` имеет тип `Nothing`, поэтому `x ?: throw IllegalArgumentException("...")` допустимо.

**`require`, `check`, `requireNotNull`, `checkNotNull`.** Лямбда с сообщением вычисляется только при нарушении условия. `require` проверяет предусловия аргументов (вина вызывающего), `check` - состояние (вина самого объекта или порядка вызовов), `error` - «сюда не должны попасть». Компилятор учитывает контракт: после `require(x != null)` значение `x` приводится к ненулевому типу.

**Собственные исключения.** `class ParseError(message: String, cause: Throwable? = null) : Exception(message, cause)`. Причину (`cause`) сохраняйте, чтобы в трассировке была вся цепочка.

**`Result<T>`.** `runCatching { ... }` ловит любое `Throwable` и возвращает `Result`. Основные методы: `getOrNull`, `getOrElse { }`, `getOrThrow`, `map`, `recover`, `fold(onSuccess, onFailure)`, `onSuccess`, `onFailure`. `Result` создан для локального использования и цепочек; в публичных API чаще применяют sealed-тип с вариантами «успех» и «ошибка»: он выразительнее и не прячет тип ошибки.

**Осторожно с `runCatching`.** Он ловит всё, включая `CancellationException` в корутинах и `Error`. Там, где отмена должна пройти дальше, перебрасывайте такие исключения.

**Ресурсы.** `use { }` у `Closeable`/`AutoCloseable` закрывает ресурс в любом случае (аналог `try-with-resources` в Java) и возвращает результат блока. Если `close()` тоже бросает, его исключение добавляется как подавленное.

<!-- section: examples -->
## Примеры

**Пример 1. `try` как выражение, `finally` и собственное исключение.**

```kotlin run
class ParseError(message: String, cause: Throwable? = null) : Exception(message, cause)

fun parseAge(s: String): Int =
    try {
        s.toInt()
    } catch (e: NumberFormatException) {
        throw ParseError("не число: $s", e)
    } finally {
        println("разобрано: $s")
    }

fun main() {
    println(parseAge("41"))
    try {
        parseAge("x")
    } catch (e: ParseError) {
        println("${e.message}, причина ${e.cause!!::class.simpleName}")
    }
    val n = try { "7".toInt() } catch (e: Exception) { -1 }
    println(n)
    val name: String? = null
    println(name ?: "нет имени")
    println(runCatching { 10 / 0 }.exceptionOrNull()!!.message)
}
```
```output
разобрано: 41
41
разобрано: x
не число: x, причина NumberFormatException
7
нет имени
/ by zero
```

**Пример 2. `require`, `check`, `error`.**

```kotlin run
class Account(private var balance: Int) {
    private var closed = false

    fun withdraw(x: Int): Int {
        require(x > 0) { "сумма должна быть положительной: $x" }
        check(!closed) { "счёт закрыт" }
        if (x > balance) error("недостаточно средств")
        balance -= x
        return balance
    }

    fun close() { closed = true }
}

fun attempt(block: () -> Any): String =
    try { "ok ${block()}" } catch (e: Exception) { "${e::class.simpleName}: ${e.message}" }

fun main() {
    val a = Account(100)
    println(attempt { a.withdraw(30) })
    println(attempt { a.withdraw(-5) })
    println(attempt { a.withdraw(500) })
    a.close()
    println(attempt { a.withdraw(1) })
}
```
```output
ok 70
IllegalArgumentException: сумма должна быть положительной: -5
IllegalStateException: недостаточно средств
IllegalStateException: счёт закрыт
```

**Пример 3. `Result` и `use`.**

```kotlin run
class Res(val name: String) : AutoCloseable {
    init { println("открыт $name") }
    override fun close() = println("закрыт $name")
}

fun parse(s: String): Result<Int> = runCatching { s.toInt() }

fun main() {
    val r1 = parse("12").map { it * 2 }
    val r2 = parse("abc").recover { -1 }
    println(r1.getOrNull())
    println(r2.getOrNull())
    println(parse("abc").getOrElse { 0 })
    println(parse("x").fold({ "ok $it" }, { "ошибка ${it::class.simpleName}" }))
    println(parse("5").isSuccess)

    val len = Res("A").use { r -> println("работаем с ${r.name}"); r.name.length }
    println(len)
    runCatching { Res("B").use { error("сбой") } }
        .onFailure { println("поймали ${it.message}") }
}
```
```output
24
-1
0
ошибка NumberFormatException
true
открыт A
работаем с A
закрыт A
1
открыт B
закрыт B
поймали сбой
```

<!-- section: mistakes -->
## Типичные ошибки

* **`catch (e: Exception)` без причины.** Ловите конкретные типы, иначе скрывается ошибка программиста.
* **Проглатывать исключение.** Пустой `catch` теряет причину; как минимум залогируйте.
* **Бросать исключение для обычного отсутствия значения.** Для этого есть `null` и `toIntOrNull`.
* **Использовать `require` для состояния объекта.** Это `check`: исключение типа `IllegalStateException` точнее описывает причину.
* **Терять `cause` при оборачивании.** Передавайте исходное исключение в конструктор.
* **`runCatching` вокруг корутинного кода.** Он поймает `CancellationException` и сломает отмену.
* **Ожидать, что `finally` изменит возвращаемое значение.** Значение блока `try` уже вычислено; перекрыть его можно только `return` в `finally`, что считается плохой практикой.
* **Ожидать проверяемых исключений.** Компилятор не заставит вас обработать ошибку: документируйте её сами.

<!-- section: summary -->
## Итоги

* Проверяемых исключений нет; `try` - выражение, `throw` имеет тип `Nothing`.
* `require` - аргументы, `check` - состояние, `error` - недостижимое.
* `runCatching` и `Result` превращают исключения в значения для цепочек, но не заменяют sealed-тип в публичном API.
* `use` закрывает ресурс при любом исходе.
* Ожидаемые «нет значения» выражайте через `null`, а не через исключение.

Сравните с [обработкой ошибок в Rust](lesson:rust.fundamentals.error-handling), [исключениями и ресурсами в C#](lesson:csharp.fundamentals.exceptions-and-resources) и [ошибками как значениями в Go](lesson:go.fundamentals.functions-and-errors). Дальше: [потоки и корутины](lesson:kotlin.fundamentals.concurrency-threads-and-coroutines).

<!-- section: facts -->
## Факты

* В Kotlin нет checked exceptions: компилятор не требует объявлять или перехватывать исключения.
* `use` закрывает ресурс и в случае исключения; исключение из `close()` подавляется, если уже было основное.
