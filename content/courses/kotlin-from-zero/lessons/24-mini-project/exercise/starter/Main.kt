data class Purchase(val product: String, val price: Int)

fun parseLine(line: String): Purchase? {
    // TODO: верните Purchase или null, если строка записана неверно
    return null
}

fun main() {
    val accepted = mutableListOf<Purchase>()
    var skipped = 0
    while (true) {
        val line = readlnOrNull() ?: break
        val purchase = parseLine(line)
        if (purchase == null) {
            skipped += 1
        } else {
            accepted.add(purchase)
        }
    }
    // TODO: напечатайте итоги в формате из условия
    println("Принято: ${accepted.size}")
}
