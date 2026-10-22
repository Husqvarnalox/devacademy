fun main() {
    val n = readln().toInt()
    val marks = mutableListOf<Int>()
    for (i in 1..n) {
        marks.add(readln().toInt())
    }
    println("Оценки по возрастанию:")
    for (mark in marks.sorted()) {
        println(mark)
    }
    println("Всего: ${marks.size}")
    println("Сумма: ${marks.sum()}")
}
