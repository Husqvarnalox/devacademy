// TODO: расширение, которое превращает "anna_maria" в "Anna Maria"
fun String.titleWords(): String = this

// TODO: добавьте параметры по умолчанию: age: Int? = null, city: String = "unknown"
// Формат: "Anna Maria, age 30, city Paris"; если возраста нет - "age n/a"
fun card(name: String): String = name

fun main() {
    val n = readln().trim().toInt()
    repeat(n) {
        val (name, rawAge, rawCity) = readln().trim().split(" ")
        val age = rawAge.toIntOrNull()
        val city = rawCity.takeIf { it != "-" }
        // TODO: если city == null, не передавайте его - пусть сработает значение по умолчанию
        println(card(name))
    }
}
