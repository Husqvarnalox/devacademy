import java.util.Locale

fun main() {
    val item = readln()
    val price = readln().toDouble()
    val quantity = readln().toInt()
    val total = price * quantity
    println("Товар: $item")
    println("Сумма: ${"%.2f".format(Locale.ROOT, total)}")
}
