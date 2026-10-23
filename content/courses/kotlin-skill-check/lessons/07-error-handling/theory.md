<!-- section: simple -->
## Что проверяет эта проверка

`try / catch / finally` как выражения, `throw`, `require`, `check`, `error`, `Result` и `runCatching`, `fold`, `getOrElse`, `exceptionOrNull`, `use`, типы исключений и их сообщения.

## Что даёт прохождение

Проверка состоит из короткого упражнения с запуском тестов и квиза на чтение кода (порог квиза - 75%). Если пройдены обе части, засчитывается урок: [Обработка ошибок: исключения, Result, require и check](lesson:kotlin.fundamentals.error-handling).

Если не пройдены, ничего не отнимается: прогресс сохраняется, а система покажет первый незавершённый урок из этого списка как место для повторения. Проверку можно пройти снова в любой момент, и открыть её можно без каких-либо условий.

<!-- section: examples -->
## Пример того, что понадобится

```kotlin run
fun age(s: String): Int {
    val n = s.trim().toInt()
    require(n in 0..120) { "возраст вне диапазона: $n" }
    return n
}

fun main() {
    for (s in listOf("25", "abc", "200")) {
        val r = runCatching { age(s) }
        println(r.fold({ "OK $it" }, { "ERR ${it::class.simpleName}: ${it.message}" }))
    }
    val balance = 5
    try {
        check(balance >= 10) { "мало средств" }
    } catch (e: IllegalStateException) {
        println(e.message)
    } finally {
        println("конец")
    }
}
```
```output
OK 25
ERR NumberFormatException: For input string: "abc"
ERR IllegalArgumentException: возраст вне диапазона: 200
мало средств
конец
```
`require` проверяет аргументы и бросает `IllegalArgumentException`, `check` проверяет состояние и бросает `IllegalStateException`; сообщение передаётся лямбдой. `runCatching` превращает любое исключение в значение `Result`, а `fold` разбирает оба исхода. `finally` выполняется в любом случае.

<!-- section: precise -->
## Что нужно уметь

* различать `require` (аргументы), `check` (состояние) и `error` и называть типы их исключений;
* использовать `try` как выражение и понимать порядок выполнения `catch` и `finally`;
* оборачивать вызов в `runCatching` и разбирать `Result` через `fold`, `getOrElse`, `exceptionOrNull`;
* читать сообщения стандартных исключений (`NumberFormatException`, `ArithmeticException`);
* закрывать ресурсы через `use`;
* знать, что исключения в Kotlin непроверяемые и что `runCatching` ловит и ошибки программиста.

<!-- section: mistakes -->
## На чём чаще всего ошибаются

* Ловить `Exception` или `Throwable` целиком и тем самым скрывать ошибки логики.
* Использовать `require` для проверки состояния объекта и `check` - для аргументов: тип исключения путает вызывающий код.
* Использовать `runCatching` там, где достаточно `toIntOrNull()` или проверки условия.
* Забывать, что `finally` выполняется даже после `return`.
* Целое деление на ноль бросает `ArithmeticException`, а дробное даёт `Infinity` и `NaN`.

<!-- section: summary -->
## Итог

Если задания показались трудными, начните с уроков из списка: [Обработка ошибок: исключения, Result, require и check](lesson:kotlin.fundamentals.error-handling). После повторения вернитесь к проверке: результат не зависит от числа попыток, а засчитанное остаётся засчитанным.
