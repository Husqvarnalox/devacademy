fun main() {
    val sum = readln().toInt()
    val delivery = if (sum >= 3000) 0 else if (sum >= 1000) 300 else 500
    println("Доставка: $delivery")
}
