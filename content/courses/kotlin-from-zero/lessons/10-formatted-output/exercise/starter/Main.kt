import java.util.Locale

fun main() {
    val item = readln()
    val price = readln().toDouble()
    val quantity = readln().toInt()
    val total = price * quantity
    println("Товар: $item")
    // TODO: напечатайте сумму с двумя знаками после точки через "%.2f".format(Locale.ROOT, total)
    println("Сумма: $total")
}
