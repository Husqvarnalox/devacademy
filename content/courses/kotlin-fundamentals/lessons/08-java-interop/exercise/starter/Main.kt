import java.util.Properties

class Config(private val props: Properties) {
    // TODO: 1) getProperty - Java-метод, его тип String! (платформенный); объявите результат как String?
    //       2) добавьте @JvmOverloads, чтобы Java видела describe(key), describe(key, width), describe(key, width, pad)
    fun describe(key: String, width: Int = 8, pad: Char = '.'): String {
        val value: String = props.getProperty(key)
        return key.padEnd(width, pad) + value
    }
}

fun main() {
    val props = Properties()
    val n = readln().trim().toInt()
    repeat(n) {
        val (k, v) = readln().trim().split(" ", limit = 2)
        props.setProperty(k, v)
    }
    val config = Config(props)
    val q = readln().trim().toInt()
    repeat(q) { println(config.describe(readln().trim())) }
    val overloads = Config::class.java.declaredMethods.count { it.name == "describe" && !it.isSynthetic }
    println("overloads=$overloads")
}
