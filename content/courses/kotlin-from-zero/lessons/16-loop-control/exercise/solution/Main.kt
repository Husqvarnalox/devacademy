fun main() {
    val n = readln().toInt()
    var sum = 0
    var skipped = 0
    for (i in 1..n) {
        val mark = readln().toInt()
        if (mark == 0) {
            break
        }
        if (mark < 1 || mark > 5) {
            skipped += 1
            continue
        }
        sum += mark
    }
    println("Сумма: $sum")
    println("Пропущено: $skipped")
}
