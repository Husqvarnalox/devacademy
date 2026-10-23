fun main() {
    var count = 0
    var sum = 0
    var price = readln().toInt()
    while (price != 0) {
        count += 1
        sum += price
        price = readln().toInt()
    }
    println("Покупок: $count")
    println("Сумма: $sum")
}
