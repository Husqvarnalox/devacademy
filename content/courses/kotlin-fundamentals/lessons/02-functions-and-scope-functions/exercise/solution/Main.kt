fun String.titleWords(): String =
    split("_").joinToString(" ") { part -> part.replaceFirstChar { it.uppercaseChar() } }

fun card(name: String, age: Int? = null, city: String = "unknown"): String =
    buildString {
        append(name.titleWords())
        append(", age ").append(age ?: "n/a")
        append(", city ").append(city)
    }

fun main() {
    val n = readln().trim().toInt()
    repeat(n) {
        val (name, rawAge, rawCity) = readln().trim().split(" ")
        val age = rawAge.toIntOrNull()
        val city = rawCity.takeIf { it != "-" }
        println(if (city == null) card(name, age) else card(name, age, city))
    }
}
