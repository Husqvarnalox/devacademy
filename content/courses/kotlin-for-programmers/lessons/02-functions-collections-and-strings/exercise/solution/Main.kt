import java.util.Locale

fun wordCounts(text: String): Map<String, Int> {
    val counts = mutableMapOf<String, Int>()
    for (w in text.lowercase().split(" ")) {
        if (w.isEmpty()) {
            continue
        }
        counts[w] = counts.getOrDefault(w, 0) + 1
    }
    return counts
}

fun topWord(counts: Map<String, Int>): String {
    var best = ""
    var bestN = 0
    for ((w, n) in counts) {
        if (n > bestN || (n == bestN && w < best)) {
            best = w
            bestN = n
        }
    }
    return if (best.isEmpty()) "-" else best
}

fun average(nums: List<Int>): Double {
    if (nums.isEmpty()) {
        return 0.0
    }
    return nums.sum().toDouble() / nums.size
}

fun titleCase(name: String): String {
    val s = name.trim()
    if (s.isEmpty()) {
        return s
    }
    return s.substring(0, 1).uppercase() + s.substring(1).lowercase()
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
