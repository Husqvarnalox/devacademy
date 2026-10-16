fun parse(token: String): Any? =
    token.toIntOrNull()
        ?: token.toDoubleOrNull()
        ?: when (token) {
            "true" -> true
            "false" -> false
            "-" -> null
            else -> token
        }

fun describe(v: Any?): String = when (v) {
    null -> "null"
    is Int -> "int:${v * 2}"
    is Double -> "double:${v * 2}"
    is Boolean -> "bool:${!v}"
    is String -> "str:${v.length}"
    else -> "other"
}

fun main() {
    val n = readln().trim().toInt()
    var sum = 0L
    repeat(n) {
        val v = parse(readln().trim())
        if (v is Int) sum += v.toLong()
        println(describe(v))
    }
    println("sum=$sum")
}
