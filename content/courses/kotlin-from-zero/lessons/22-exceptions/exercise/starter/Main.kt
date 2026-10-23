fun checkAge(age: Int): Int {
    // TODO: если age меньше 0 или больше 120, выбросьте
    // IllegalArgumentException("Возраст должен быть от 0 до 120")
    return age
}

fun main() {
    val n = readln().toInt()
    for (i in 1..n) {
        val text = readln()
        try {
            val age = checkAge(text.toInt())
            println("Принято: $age")
        } catch (e: NumberFormatException) {
            println("Не число: $text")
        } catch (e: IllegalArgumentException) {
            println("Ошибка: ${e.message}")
        }
    }
}
