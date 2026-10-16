// TODO: reified-параметр позволяет проверить `it is T` внутри inline-функции
inline fun <reified T> List<Any>.countOf(): Int = 0

// TODO: ограничение T : Comparable<T> позволяет сравнивать элементы через >
fun <T : Comparable<T>> List<T>.maxOrNone(): T? = null

// TODO: List<out E> ковариантен, поэтому сюда можно передать List<Int> и List<Double>
fun sumAll(xs: List<Number>): Double = 0.0

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
