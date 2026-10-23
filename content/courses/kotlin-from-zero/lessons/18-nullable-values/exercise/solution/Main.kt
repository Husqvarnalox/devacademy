fun main() {
    var accepted = 0
    var errors = 0
    var total = 0
    while (true) {
        val line = readlnOrNull() ?: break
        val amount = line.toIntOrNull()
        if (amount == null) {
            errors += 1
        } else {
            accepted += 1
            total += amount
        }
    }
    println("Принято: $accepted")
    println("Ошибок: $errors")
    println("Итого: $total")
}
