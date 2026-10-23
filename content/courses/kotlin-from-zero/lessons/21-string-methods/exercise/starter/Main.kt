fun main() {
    val parts = readln().split(",")
    val numbers = mutableListOf<Int>()
    // TODO: для каждой части очистите пробелы через trim(), преобразуйте в Int и добавьте в numbers
    println("Чисел: ${numbers.size}")
    println("Сумма: ${numbers.sum()}")
    println("Запись: ${numbers.joinToString(" + ")} = ${numbers.sum()}")
}
