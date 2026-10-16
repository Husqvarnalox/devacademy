fun domain(email: String): String? {
    if (email == "-") return null
    // TODO: верните часть после '@' в верхнем регистре или null, если '@' нет
    return email
}

fun main() {
    val n = readln().trim().toInt()
    repeat(n) {
        val parts = readln().trim().split(" ")
        val name = parts[0]
        val d = domain(parts[1])
        // TODO: уберите !! и подставьте "NONE" для null
        println("$name: ${d!!}")
    }
}
