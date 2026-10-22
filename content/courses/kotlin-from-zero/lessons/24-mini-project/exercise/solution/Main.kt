data class Purchase(val product: String, val price: Int)

fun parseLine(line: String): Purchase? {
    val parts = line.split(";")
    if (parts.size != 2) {
        return null
    }
    val product = parts[0].trim()
    val price = parts[1].trim().toIntOrNull()
    if (product == "" || price == null || price < 0) {
        return null
    }
    return Purchase(product, price)
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
    var sum = 0
    var bestName = ""
    var bestPrice = -1
    val totals = mutableMapOf<String, Int>()
    for (purchase in accepted) {
        sum += purchase.price
        totals[purchase.product] = (totals[purchase.product] ?: 0) + purchase.price
        if (purchase.price > bestPrice) {
            bestName = purchase.product
            bestPrice = purchase.price
        }
    }
    println("Принято: ${accepted.size}")
    println("Пропущено: $skipped")
    println("Сумма: $sum")
    if (bestPrice == -1) {
        println("Самая дорогая: нет")
    } else {
        println("Самая дорогая: $bestName ($bestPrice)")
    }
    println("По товарам:")
    for ((product, total) in totals) {
        println("$product: $total")
    }
}
