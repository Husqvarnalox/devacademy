inline fun <reified T> List<Any>.countOf(): Int = count { it is T }

fun <T : Comparable<T>> List<T>.maxOrNone(): T? {
    var best: T? = null
    for (x in this) if (best == null || x > best) best = x
    return best
}

fun sumAll(xs: List<Number>): Double = xs.sumOf { it.toDouble() }

fun main() {
    val n = readln().trim().toInt()
    val items = ArrayList<Any>()
    repeat(n) {
        val token = readln().trim()
        items += token.toIntOrNull() ?: token.toDoubleOrNull() ?: token
    }
    val ints = items.filterIsInstance<Int>()
    val doubles = items.filterIsInstance<Double>()
    val strings = items.filterIsInstance<String>()
    println("ints=${items.countOf<Int>()} doubles=${items.countOf<Double>()} strings=${items.countOf<String>()}")
    println("max-int=${ints.maxOrNone() ?: "none"}")
    println("max-str=${strings.maxOrNone() ?: "none"}")
    val numbers: List<Number> = ints + doubles
    println("numsum=${sumAll(numbers)}")
}
