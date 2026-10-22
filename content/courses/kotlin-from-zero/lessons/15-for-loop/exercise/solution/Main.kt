fun main() {
    val amount = readln().toInt()
    val days = readln().toInt()
    for (day in 1..days) {
        println("День $day: накоплено ${amount * day}")
    }
    println("Итого: ${amount * days}")
}
