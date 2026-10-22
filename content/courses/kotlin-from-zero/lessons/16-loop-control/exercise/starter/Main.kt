fun main() {
    val n = readln().toInt()
    var sum = 0
    var skipped = 0
    for (i in 1..n) {
        val mark = readln().toInt()
        // TODO: если mark равна 0 - прервите цикл (break);
        // если оценка меньше 1 или больше 5 - увеличьте skipped и пропустите шаг (continue);
        // иначе прибавьте оценку к sum
    }
    println("Сумма: $sum")
    println("Пропущено: $skipped")
}
