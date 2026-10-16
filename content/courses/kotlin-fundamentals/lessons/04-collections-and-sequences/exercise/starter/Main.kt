fun main() {
    val k = readln().trim().toInt()
    val words = generateSequence(::readlnOrNull)
        .flatMap { it.lowercase().split(" ", "\t").asSequence() }
        .filter { it.isNotEmpty() }
        .toList()
    // TODO: подсчитайте частоты слов (groupingBy + eachCount), отсортируйте
    // по убыванию частоты, затем по слову, и выведите первые k как "слово число"
    words.take(k).forEach { println("$it 1") }
}
