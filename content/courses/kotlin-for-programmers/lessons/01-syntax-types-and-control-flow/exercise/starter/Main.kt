import kotlin.math.roundToInt

// Стоимость priceCents * qty как Long (без переполнения Int)
fun totalCents(priceCents: Int, qty: Int): Long {
    return 0L // TODO
}

// Доля part от total в процентах, округление половины вверх
fun percent(part: Int, total: Int): Int {
    return 0 // TODO
}

// Сдвиг строчных латинских букв по кругу на shift позиций
fun caesar(word: String, shift: Int): String {
    return word // TODO
}

// "weekend" для sat и sun, "weekday" для mon-fri, иначе "unknown"
fun dayType(day: String): String {
    return "unknown" // TODO
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
