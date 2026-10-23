fun addLine(totals: MutableMap<String, Int>, line: String) {
    val parts = line.split(" ")
    val name = parts[0]
    val qty = parts[1].toInt()
    totals[name] = totals.getOrDefault(name, 0) + qty
}

fun report(totals: Map<String, Int>): List<String> {
    val lines = mutableListOf<String>()
    var all = 0
    for (name in totals.keys.sorted()) {
        val qty = totals.getValue(name)
        lines.add("$name: $qty")
        all += qty
    }
    lines.add("Всего: $all")
    return lines
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
