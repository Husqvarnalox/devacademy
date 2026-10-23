import kotlinx.coroutines.*
import java.util.concurrent.atomic.AtomicInteger

val active = AtomicInteger(0)
val maxActive = AtomicInteger(0)

suspend fun slowSquare(x: Int): Int {
    val now = active.incrementAndGet()
    maxActive.updateAndGet { m -> if (now > m) now else m }
    delay(50)
    active.decrementAndGet()
    return x * x
}

suspend fun squares(xs: List<Int>): List<Int> = coroutineScope {
    xs.map { async { slowSquare(it) } }.awaitAll()
}

fun main() = runBlocking {
    val xs = readln().split(" ").filter { it.isNotEmpty() }.map { it.toInt() }
    val result = squares(xs)
    println("Квадраты: $result")
    println("Сумма: ${result.sum()}")
    println("Одновременно: ${maxActive.get()}")
}
