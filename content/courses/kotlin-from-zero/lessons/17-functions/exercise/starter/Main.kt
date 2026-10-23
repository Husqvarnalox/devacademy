// Цена билета в музей по возрасту:
// до 7 лет (не включая) - бесплатно (0), от 7 до 17 лет - 200,
// от 65 лет и старше - 250, все остальные - 400.
fun ticketPrice(age: Int): Int {
    // TODO: верните цену билета для возраста age
    return 0
}

fun main() {
    val n = readln().toInt()
    for (i in 1..n) {
        val age = readln().toInt()
        println("Возраст $age: ${ticketPrice(age)}")
    }
}
