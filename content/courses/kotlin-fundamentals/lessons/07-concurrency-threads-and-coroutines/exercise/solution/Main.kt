import java.util.concurrent.atomic.AtomicInteger
import kotlin.concurrent.thread

fun main() {
    val (n, k) = readln().trim().split(" ").map { it.toInt() }
    val nums = readln().trim().split(" ").filter { it.isNotEmpty() }.map { it.toLong() }
    check(nums.size == n)
    val sums = LongArray(k)
    val finished = AtomicInteger()
    val workers = (0 until k).map { i ->
        thread {
            val from = i * n / k
            val to = (i + 1) * n / k
            var s = 0L
            for (j in from until to) s += nums[j]
            sums[i] = s
            finished.incrementAndGet()
        }
    }
    workers.forEach { it.join() }
    sums.forEachIndexed { i, s -> println("chunk $i: $s") }
    println("total=${sums.sum()}")
    println("finished=${finished.get()}")
}
