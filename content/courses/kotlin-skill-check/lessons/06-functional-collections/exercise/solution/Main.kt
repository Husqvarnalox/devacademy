fun evenSquares(xs: List<Int>): List<Int> {
    return xs.filter { it % 2 == 0 }.map { it * it }
}

fun groupByLength(words: List<String>): Map<Int, List<String>> {
    return words.groupBy { it.length }
}

fun firstSquareAbove(limit: Int): Int {
    return generateSequence(1) { it + 1 }.map { it * it }.first { it > limit }
}

fun main() {
    val xs = readln().split(" ").filter { it.isNotEmpty() }.map { it.toInt() }
    val words = readln().split(" ").filter { it.isNotEmpty() }
    val limit = readln().toInt()
    println("Чётные квадраты: ${evenSquares(xs)}")
    for ((len, ws) in groupByLength(words).toSortedMap()) {
        println("$len: $ws")
    }
    println("Первый квадрат больше $limit: ${firstSquareAbove(limit)}")
}
