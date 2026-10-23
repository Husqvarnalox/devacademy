import java.util.Locale

// Частоты слов (без учёта регистра), порядок первого появления
fun wordCounts(text: String): Map<String, Int> {
    return emptyMap() // TODO
}

// Слово с наибольшей частотой; при равенстве - раньше по алфавиту; для пустого словаря "-"
fun topWord(counts: Map<String, Int>): String {
    return "-" // TODO
}

// Среднее арифметическое; для пустого списка 0.0
fun average(nums: List<Int>): Double {
    return 0.0 // TODO
}

// Имя без пробелов по краям: первая буква заглавная, остальные строчные
fun titleCase(name: String): String {
    return name // TODO
}

fun main() {
    val text = readln()
    val nums = readln().split(" ").filter { it.isNotEmpty() }.map { it.toInt() }
    val name = readln()
    val counts = wordCounts(text)
    val top = topWord(counts)
    println("Уникальных слов: ${counts.size}")
    println("Чаще всего: $top (${counts[top] ?: 0})")
    println("Среднее: ${"%.2f".format(Locale.ROOT, average(nums))}")
    println("Имя: ${titleCase(name)}")
}
