fun main() {
    val n = readln().toInt()
    var sum = 0
    var even = 0
    var max = 0
    for (i in 1..n) {
        val x = readln().toInt()
        sum += x
        if (x % 2 == 0) {
            even++
        }
        if (i == 1 || x > max) {
            max = x
        }
    }
    val average = if (n > 0) sum / n else 0
    println("Чисел: $n")
    println("Сумма: $sum")
    println("Чётных: $even")
    println("Среднее: $average")
    println("Максимум: $max")
}
