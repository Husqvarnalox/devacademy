fun main() {
    val age = readln().toInt()
    val answer = readln()
    val allowed = age >= 18 && answer == "да"
    println("Допущен: $allowed")
}
