<!-- section: simple -->
## Что проверяет эта проверка

Типы `T` и `T?`, безопасный вызов `?.`, оператор `?:`, проверка `!= null` и smart cast, `toIntOrNull()`, `orEmpty()`, `?.let`, платформенные типы Java и опасность `!!`.

## Что даёт прохождение

Проверка состоит из короткого упражнения с запуском тестов и квиза на чтение кода (порог квиза - 75%). Если пройдены обе части, засчитываются уроки: [Null-безопасность: ?, ?., ?: и !!](lesson:kotlin.first-steps.null-safety-and-val-var), [Типы, val/var и null-безопасность](lesson:kotlin.fundamentals.types-and-null-safety).

Если не пройдены, ничего не отнимается: прогресс сохраняется, а система покажет первый незавершённый урок из этого списка как место для повторения. Проверку можно пройти снова в любой момент, и открыть её можно без каких-либо условий.

<!-- section: examples -->
## Пример того, что понадобится

```kotlin run
fun describe(s: String?): String {
    if (s == null) {
        return "нет значения"
    }
    return "длина ${s.length}"
}

fun main() {
    val a: String? = "kotlin"
    val b: String? = null
    println("${a?.length} ${b?.length} ${b?.length ?: -1}")
    println(describe(a))
    println(describe(b))
    println("${a?.let { it.uppercase() }} ${b?.let { it.uppercase() }}")
    println("${b.orEmpty().length} ${"42".toIntOrNull() ?: 0} ${"x".toIntOrNull() ?: 0}")
    try {
        println(b!!.length)
    } catch (e: NullPointerException) {
        println("NPE")
    }
}
```
```output
6 null -1
длина 6
нет значения
KOTLIN null
0 42 0
NPE
```
`?.` возвращает `null`, если слева `null`; `?:` подставляет значение по умолчанию. После проверки `s == null` с выходом из функции компилятор знает, что `s` не `null`, и разрешает `s.length` (smart cast). `?.let { ... }` выполняет блок только для ненулевого значения. Оператор `!!` вместо проверки превращает `null` в `NullPointerException`.

<!-- section: precise -->
## Что нужно уметь

* различать `T` и `T?` и объяснять, почему компилятор не принимает `null` для `T`;
* применять `?.`, `?:` и `orEmpty()` вместо проверок `if (x != null)`, где это короче и понятнее;
* понимать, когда работает smart cast, и почему он не работает для изменяемого свойства класса;
* заменять `!!` на `?:`, `?.let` или явную проверку и называть места, где `NullPointerException` всё же возможен;
* получать `null` из `toIntOrNull()`, `map[key]`, `readlnOrNull()`, `firstOrNull()` и обрабатывать его;
* объяснять, что значение из Java имеет платформенный тип без проверки на `null`.

<!-- section: mistakes -->
## На чём чаще всего ошибаются

* `s!!.length` на пустом значении бросает `NullPointerException`.
* Вызов `s.length` на `String?` без `?.` не компилируется.
* Smart cast недоступен для `var`-свойства класса, которое могло измениться между проверкой и использованием.
* `s?.length` возвращает `Int?`, а не `Int`: результат нужно обработать через `?:`.
* Возвращаемое значение Java-метода принимают как `String`, хотя оно может быть `null`: исключение возникает на присваивании.

<!-- section: summary -->
## Итог

Если задания показались трудными, начните с уроков из списка: [Null-безопасность: ?, ?., ?: и !!](lesson:kotlin.first-steps.null-safety-and-val-var), [Типы, val/var и null-безопасность](lesson:kotlin.fundamentals.types-and-null-safety). После повторения вернитесь к проверке: результат не зависит от числа попыток, а засчитанное остаётся засчитанным.
