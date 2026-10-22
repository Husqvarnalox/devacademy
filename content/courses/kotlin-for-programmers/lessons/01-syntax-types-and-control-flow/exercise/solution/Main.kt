import kotlin.math.roundToInt

fun totalCents(priceCents: Int, qty: Int): Long {
    return priceCents.toLong() * qty
}

fun percent(part: Int, total: Int): Int {
    return (part * 100.0 / total).roundToInt()
}

fun caesar(word: String, shift: Int): String {
    val result = StringBuilder()
    for (c in word) {
        if (c in 'a'..'z') {
            result.append('a' + (c - 'a' + shift).mod(26))
        } else {
            result.append(c)
        }
    }
    return result.toString()
}

fun dayType(day: String): String = when (day) {
    "sat", "sun" -> "weekend"
    "mon", "tue", "wed", "thu", "fri" -> "weekday"
    else -> "unknown"
}

fun main() {
    val priceQty = readln().split(" ")
    val partTotal = readln().split(" ")
    val wordShift = readln().split(" ")
    val day = readln()
    println("Сумма: ${totalCents(priceQty[0].toInt(), priceQty[1].toInt())}")
    println("Доля: ${percent(partTotal[0].toInt(), partTotal[1].toInt())}%")
    println("Шифр: ${caesar(wordShift[0], wordShift[1].toInt())}")
    println("День: ${dayType(day)}")
}
