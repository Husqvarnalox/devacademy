fun main() {
    val n = readln().toInt()
    val counts = mutableMapOf<String, Int>()
    for (i in 1..n) {
        val product = readln()
        // TODO: увеличьте счётчик для product: counts[product] = (counts[product] ?: 0) + 1
    }
    // TODO: для каждой пары напечатайте "товар: количество"
    println("Разных товаров: ${counts.size}")
}
