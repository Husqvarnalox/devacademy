<!-- section: simple -->
## Конкурентность: потоки, синхронизация и корутины

**Как устроена среда запуска.** Библиотека `kotlinx.coroutines` не входит в стандартную библиотеку Kotlin, её обычно подключают как зависимость сборки. Приложение DevAcademy ничего не скачивает: оно использует `kotlinx-coroutines-core-jvm.jar`, который лежит в дистрибутиве `kotlinc`, и добавляет его в classpath при компиляции и запуске. Поэтому в этом уроке:

* потоки JVM, `kotlin.concurrent`, `java.util.concurrent` и `lazy` **запускаются**;
* корутины тоже **запускаются** (`import kotlinx.coroutines.*`): все выводы в примерах получены реальным запуском.

Если у вас на машине установлен `kotlinc` без этого файла, программы с `kotlinx.coroutines` не скомпилируются: понадобится дистрибутив, в который он входит (официальный архив Kotlin его включает). Остальные примеры урока от него не зависят.

Идея конкурентности одна: несколько задач выполняются «одновременно» и должны безопасно делить данные.

* **Поток** (`Thread`) - тяжёлая единица планирования ОС. `kotlin.concurrent.thread { ... }` запускает его, `join()` ждёт завершения.
* **Общее изменяемое состояние** - источник гонок. Безопасные способы: `@Volatile` для флагов, `AtomicInteger`/`AtomicLong`, `synchronized`/`ReentrantLock.withLock`, потокобезопасные коллекции (`ConcurrentHashMap`).
* **Корутина** - лёгкая «приостанавливаемая» задача: функция с модификатором `suspend` может освободить поток в точке ожидания (`delay`, ввод-вывод) и продолжить позже, возможно в другом потоке. Тысячи корутин не требуют тысяч потоков.
* **Структурированная конкурентность**: корутины запускаются внутри области (scope), и область не завершится, пока не завершатся все её дочерние корутины. Ошибка или отмена распространяются по иерархии.

<!-- section: precise -->
## Подробнее

**Гонки данных.** `counter++` - три шага (чтение, сложение, запись), поэтому два потока теряют обновления. Решение: `AtomicInteger.incrementAndGet()` или `synchronized(lock) { counter++ }`. Видимость между потоками без синхронизации не гарантируется: флаг остановки помечают `@Volatile`.

**`join` и порядок.** Результаты потоков нельзя читать до `join()`: до этого они могут быть не записаны. Порядок выполнения потоков не определён, поэтому детерминированный результат получают, собирая значения по индексам или после `join`, а не по моменту завершения.

**Пул потоков.** `Executors.newFixedThreadPool(n)` принимает задачи (`submit`) и возвращает `Future`; `future.get()` блокирует до результата. Пул нужно завершать (`shutdown()`), иначе программа не выйдет.

**`lazy`.** `val x by lazy { ... }` вычисляет значение один раз при первом обращении; по умолчанию потокобезопасно (режим `SYNCHRONIZED`).

**Корутины.**
* `suspend fun` можно вызывать только из другой `suspend`-функции или из билдера корутины.
* Билдеры: `launch` (запустить и забыть, возвращает `Job`), `async` (результат через `await()`, возвращает `Deferred`), `runBlocking` (блокирует текущий поток; для `main` и тестов).
* `coroutineScope { }` создаёт область и ждёт всех дочерних корутин; ошибка в одном ребёнке отменяет остальных и пробрасывается наружу.
* Отмена кооперативная: корутина проверяет её в точках приостановки (`delay`, `yield`) или через `isActive`.
* Диспетчеры (`Dispatchers.Default`, `IO`, `Main`) определяют, на каких потоках выполняются корутины.
* `delay` приостанавливает корутину, `Thread.sleep` блокирует поток: разница принципиальна.
* `Flow` - холодный асинхронный поток значений; `Channel` - очередь между корутинами. Оба относятся к `kotlinx.coroutines`.

**Связь с другими языками.** В C# похожую роль играет [async/await](lesson:csharp.fundamentals.async-await), в Go - [горутины и каналы](lesson:go.fundamentals.goroutines-and-channels), в Rust - [модель потоков с проверкой владения](lesson:rust.fundamentals.concurrency-fearless).

<!-- section: examples -->
## Примеры

**Пример 1. Потоки, `join` и атомарный счётчик.**

```kotlin run
import java.util.concurrent.atomic.AtomicInteger
import kotlin.concurrent.thread

fun main() {
    val atomic = AtomicInteger()
    var plain = 0
    val lock = Any()
    val workers = List(4) {
        thread {
            repeat(1000) {
                atomic.incrementAndGet()
                synchronized(lock) { plain++ }
            }
        }
    }
    workers.forEach { it.join() }
    println(atomic.get())
    println(plain)

    val results = IntArray(3)
    val t = (0 until 3).map { i -> thread { results[i] = (i + 1) * 100 } }
    t.forEach { it.join() }
    println(results.toList())
}
```
```output
4000
4000
[100, 200, 300]
```

**Пример 2. Пул потоков, `Future` и `withLock`.**

```kotlin run
import java.util.concurrent.Callable
import java.util.concurrent.Executors
import java.util.concurrent.locks.ReentrantLock
import kotlin.concurrent.withLock

fun main() {
    val pool = Executors.newFixedThreadPool(3)
    val futures = (1..5).map { n -> pool.submit(Callable { n * n }) }
    println(futures.map { it.get() })
    pool.shutdown()

    val lock = ReentrantLock()
    var shared = 0
    val threads = List(3) { kotlin.concurrent.thread { repeat(500) { lock.withLock { shared++ } } } }
    threads.forEach { it.join() }
    println(shared)
}
```
```output
[1, 4, 9, 16, 25]
1500
```

**Пример 3. `lazy` и безопасная публикация результата.**

```kotlin run
import kotlin.concurrent.thread

val config: Map<String, Int> by lazy {
    println("инициализация (один раз)")
    mapOf("a" to 1, "b" to 2)
}

fun main() {
    val sums = LongArray(4)
    val workers = List(4) { i -> thread { sums[i] = config.values.sum().toLong() + i } }
    workers.forEach { it.join() }
    println(sums.toList())
}
```
```output
инициализация (один раз)
[3, 4, 5, 6]
```

**Пример 4. `async` и `await`.** Две задачи выполняются конкурентно, `delay` не блокирует поток. Порядок вывода определён: `await` возвращает результаты в том порядке, в каком их запрашивают, а `job.join()` ждёт дочернюю корутину.

```kotlin run
import kotlinx.coroutines.*

suspend fun fetch(id: Int): String {
    delay(10L * id)            // приостанавливает корутину, не блокируя поток
    return "данные-$id"
}

fun main() = runBlocking {
    val a = async { fetch(3) }
    val b = async { fetch(1) }
    println(listOf(a.await(), b.await()))

    val job = launch {
        println("дочерняя корутина")
    }
    job.join()
    println("после join")
}
```

```output
[данные-3, данные-1]
дочерняя корутина
после join
```

**Пример 5. Структурированная конкурентность и отмена.**

```kotlin run
import kotlinx.coroutines.*

fun main() = runBlocking {
    try {
        coroutineScope {
            launch {
                delay(500)
                println("не выполнится: её отменит соседняя ошибка")
            }
            launch {
                delay(10)
                throw IllegalStateException("сбой в одной дочерней")
            }
        }
    } catch (e: IllegalStateException) {
        println("scope завершился с: ${e.message}")
    }

    val scope = CoroutineScope(Job())
    val child = scope.launch { delay(10_000) }
    scope.cancel()
    child.join()
    println("отменена: ${child.isCancelled}")
}
```

```output
scope завершился с: сбой в одной дочерней
отменена: true
```

Первая корутина отменена вместе с областью, поэтому «не выполнится...» не напечатано.

<!-- section: mistakes -->
## Типичные ошибки

* **Изменять общую переменную из нескольких потоков без синхронизации.** Обновления теряются; используйте атомики или блокировки.
* **Читать результат до `join()`.** Данные могут быть ещё не записаны.
* **Полагаться на порядок завершения потоков.** Он не определён; собирайте результаты по индексу.
* **Забыть `shutdown()` у пула.** Программа не завершится.
* **Блокирующий вызов в корутине.** `Thread.sleep` держит поток; нужен `delay`.
* **Использовать `GlobalScope.launch`.** Такая корутина живёт вне иерархии, ошибки и отмена не распространяются: запускайте корутины в явной области.
* **Ловить `CancellationException` и глотать её.** Отмена перестанет работать.
* **Ждать, что `runBlocking` годится для обычного кода.** Он блокирует поток и предназначен для `main` и тестов.

<!-- section: summary -->
## Итоги

* Потоки JVM доступны всегда: `thread { }`, `join()`, `AtomicInteger`, `synchronized`, `withLock`, пулы.
* Результат работы потоков безопасно читать после `join()`; порядок выполнения не определён.
* Корутины - лёгкие приостанавливаемые задачи; им нужна библиотека `kotlinx.coroutines`, и приложение берёт её из дистрибутива `kotlinc`, поэтому корутины в уроке запускаются.
* Структурированная конкурентность: область ждёт всех детей, ошибка отменяет остальных, отмена кооперативная.

Дальше: [взаимодействие с Java](lesson:kotlin.fundamentals.java-interop).

<!-- section: facts -->
## Факты

* `kotlinx.coroutines` - отдельная библиотека, а не часть стандартной библиотеки Kotlin; приложение подключает её из дистрибутива `kotlinc`.
* `lazy` по умолчанию потокобезопасен.
