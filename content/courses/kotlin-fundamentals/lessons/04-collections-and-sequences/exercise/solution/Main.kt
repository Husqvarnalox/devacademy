fun main() {
    val k = readln().trim().toInt()
    val words = generateSequence(::readlnOrNull)
        .flatMap { it.lowercase().split(" ", "\t").asSequence() }
        .filter { it.isNotEmpty() }
        .toList()
    words.groupingBy { it }.eachCount().entries
        .sortedWith(compareByDescending<Map.Entry<String, Int>> { it.value }.thenBy { it.key })
        .take(k)
        .forEach { println("${it.key} ${it.value}") }
}
