fun addLine(totals: MutableMap<String, Int>, line: String) {
    // TODO: разберите строку "товар количество" и прибавьте количество к итогу товара
}

fun report(totals: Map<String, Int>): List<String> {
    // TODO: строки "товар: итог" по алфавиту и последняя строка "Всего: ..."
    return emptyList()
}

fun main() {
    val n = readln().toInt()
    val totals = mutableMapOf<String, Int>()
    for (i in 1..n) {
        addLine(totals, readln())
    }
    for (line in report(totals)) {
        println(line)
    }
}
