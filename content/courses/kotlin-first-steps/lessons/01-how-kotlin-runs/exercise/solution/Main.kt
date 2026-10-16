import java.util.Locale

fun main() {
    val words = System.`in`.bufferedReader().readText()
        .split(Regex("\\s+")).filter { it.isNotEmpty() }
    val n = words[0].toInt()
    val nums = words.drop(1).take(n).map { it.toLong() }
    val sum = nums.sum()
    println("sum=$sum")
    println("min=${nums.min()}")
    println("max=${nums.max()}")
    println("avg=" + "%.2f".format(Locale.ROOT, sum.toDouble() / n))
}
