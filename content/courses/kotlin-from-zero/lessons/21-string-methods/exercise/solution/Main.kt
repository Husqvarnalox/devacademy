fun main() {
    val parts = readln().split(",")
    val numbers = mutableListOf<Int>()
    for (part in parts) {
        numbers.add(part.trim().toInt())
    }
    println("Чисел: ${numbers.size}")
    println("Сумма: ${numbers.sum()}")
    println("Запись: ${numbers.joinToString(" + ")} = ${numbers.sum()}")
}
