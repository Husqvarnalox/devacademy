import java.util.concurrent.atomic.AtomicInteger
import kotlin.concurrent.thread

fun main() {
    val (n, k) = readln().trim().split(" ").map { it.toInt() }
    val nums = readln().trim().split(" ").filter { it.isNotEmpty() }.map { it.toLong() }
    check(nums.size == n)
    val sums = LongArray(k)
    val finished = AtomicInteger()
    // TODO: запустите k потоков (kotlin.concurrent.thread). Поток i суммирует
    // элементы с индексами от i*n/k (включительно) до (i+1)*n/k (исключительно),
    // кладёт сумму в sums[i] и увеличивает finished.
    // Не забудьте дождаться всех потоков через join().
    sums.forEachIndexed { i, s -> println("chunk $i: $s") }
    println("total=${sums.sum()}")
    println("finished=${finished.get()}")
}
