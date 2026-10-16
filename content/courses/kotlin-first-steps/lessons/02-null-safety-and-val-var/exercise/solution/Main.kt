fun domain(email: String): String? {
    if (email == "-") return null
    return email.takeIf { '@' in it }?.substringAfter('@')?.uppercase()
}

fun main() {
    val n = readln().trim().toInt()
    repeat(n) {
        val parts = readln().trim().split(" ")
        val name = parts[0]
        val d = domain(parts[1]) ?: "NONE"
        println("$name: $d")
    }
}
