fun ticketPrice(age: Int): Int {
    if (age < 7) {
        return 0
    }
    if (age < 18) {
        return 200
    }
    if (age >= 65) {
        return 250
    }
    return 400
}

fun main() {
    val n = readln().toInt()
    for (i in 1..n) {
        val age = readln().toInt()
        println("Возраст $age: ${ticketPrice(age)}")
    }
}
