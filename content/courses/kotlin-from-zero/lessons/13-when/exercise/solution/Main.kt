fun main() {
    val month = readln().toInt()
    when (month) {
        1, 3, 5, 7, 8, 10, 12 -> println("Дней: 31")
        4, 6, 9, 11 -> println("Дней: 30")
        2 -> println("Дней: 28")
        else -> println("Нет такого месяца")
    }
}
