// Квадраты чётных чисел
fun evenSquares(xs: List<Int>): List<Int> {
    return emptyList() // TODO
}

// Слова, сгруппированные по длине
fun groupByLength(words: List<String>): Map<Int, List<String>> {
    return emptyMap() // TODO
}

// Наименьший квадрат натурального числа, больший limit (через последовательность)
fun firstSquareAbove(limit: Int): Int {
    return 0 // TODO
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
