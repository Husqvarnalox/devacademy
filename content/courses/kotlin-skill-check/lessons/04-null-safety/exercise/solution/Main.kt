fun safeLength(s: String?): Int = s?.length ?: -1

fun firstWord(s: String?): String {
    val t = s?.trim() ?: return "?"
    if (t.isEmpty()) {
        return "?"
    }
    return t.split(" ")[0]
}

fun parsePort(s: String?): Int {
    val n = s?.trim()?.toIntOrNull() ?: return 8080
    return if (n in 1..65535) n else 8080
}

fun main() {
    while (true) {
        val line = readlnOrNull() ?: break
        val s: String? = if (line == "-") null else line
        println("${safeLength(s)} ${firstWord(s)} ${parsePort(s)}")
    }
}
