import java.util.Locale

fun main() {
    val words = System.`in`.bufferedReader().readText()
        .split(Regex("\\s+")).filter { it.isNotEmpty() }
    val n = words[0].toInt()
    var sum = 0L
    var min = 0L
    var max = 0L
    // TODO: пройдите по n числам (Long), обновите sum, min, max
    println("sum=$sum")
    println("min=$min")
    println("max=$max")
    println("avg=" + "%.2f".format(Locale.ROOT, 0.0))
}
