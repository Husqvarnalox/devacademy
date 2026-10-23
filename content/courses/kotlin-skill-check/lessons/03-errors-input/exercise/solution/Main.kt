fun main() {
    var accepted = 0
    var notNumber = 0
    var outOfRange = 0
    var sum = 0
    while (true) {
        val line = readlnOrNull() ?: break
        val n = line.trim().toIntOrNull()
        if (n == null) {
            notNumber++
        } else if (n < 0 || n > 100) {
            outOfRange++
        } else {
            accepted++
            sum += n
        }
    }
    println("Принято: $accepted")
    println("Не числа: $notNumber")
    println("Вне диапазона: $outOfRange")
    println("Сумма: $sum")
}
