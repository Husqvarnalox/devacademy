data class Order(val item: String, val qty: Int)

fun parseOrder(line: String): Order? {
    val parts = line.split(";")
    if (parts.size != 2) {
        return null
    }
    val item = parts[0].trim()
    val qty = parts[1].trim().toIntOrNull() ?: return null
    if (item.isEmpty()) {
        return null
    }
    return Order(item, qty)
}

fun checkOrder(order: Order): Order {
    if (order.qty < 1 || order.qty > 100) {
        throw IllegalArgumentException("Количество должно быть от 1 до 100: ${order.qty}")
    }
    return order
}

fun addMore(order: Order, extra: Int): Order {
    return order.copy(qty = order.qty + extra)
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
