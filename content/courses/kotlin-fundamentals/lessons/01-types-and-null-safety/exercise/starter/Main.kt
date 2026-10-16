// Токен: целое, дробное, true/false, "-" (значит null) или строка.
fun parse(token: String): Any? {
    // TODO: сначала toIntOrNull, затем toDoubleOrNull, затем true/false, "-" -> null, иначе строка
    return token
}

fun describe(v: Any?): String {
    // TODO: Int -> "int:<x*2>", Double -> "double:<x*2>", Boolean -> "bool:<!x>",
    //       String -> "str:<длина>", null -> "null"
    return "other"
}

fun main() {
    val n = readln().trim().toInt()
    var sum = 0 // TODO: сумма должна быть Long, иначе переполнение
    repeat(n) {
        val v = parse(readln().trim())
        // TODO: прибавьте v к sum, если это Int (smart cast)
        println(describe(v))
    }
    println("sum=$sum")
}
