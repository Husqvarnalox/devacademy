// Длина строки или -1 для null
fun safeLength(s: String?): Int {
    return 0 // TODO
}

// Первое слово после trim(); "?" для null или пустой строки
fun firstWord(s: String?): String {
    return "" // TODO
}

// Порт 1..65535 из строки, иначе 8080
fun parsePort(s: String?): Int {
    return 0 // TODO
}

fun main() {
    while (true) {
        val line = readlnOrNull() ?: break
        val s: String? = if (line == "-") null else line
        println("${safeLength(s)} ${firstWord(s)} ${parsePort(s)}")
    }
}
