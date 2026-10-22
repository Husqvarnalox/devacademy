data class Order(val item: String, val qty: Int)

// "товар;количество" -> Order, для неверной строки null
fun parseOrder(line: String): Order? {
    return null // TODO
}

// количество от 1 до 100, иначе IllegalArgumentException
fun checkOrder(order: Order): Order {
    return order // TODO
}

// новый заказ с количеством, увеличенным на extra
fun addMore(order: Order, extra: Int): Order {
    return order // TODO
}

fun main() {
    val orders = mutableMapOf<String, Order>()
    var bad = 0
    var rejected = 0
    while (true) {
        val line = readlnOrNull() ?: break
        val parsed = parseOrder(line)
        if (parsed == null) {
            bad++
            continue
        }
        try {
            val ok = checkOrder(parsed)
            val prev = orders[ok.item]
            orders[ok.item] = if (prev == null) ok else addMore(prev, ok.qty)
        } catch (e: IllegalArgumentException) {
            println("Отклонено: ${e.message}")
            rejected++
        }
    }
    for (o in orders.values) {
        println(o)
    }
    println("Неверных строк: $bad")
    println("Отклонено заказов: $rejected")
}
