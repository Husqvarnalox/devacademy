import java.io.IOException
import java.lang.reflect.Method
import java.lang.reflect.Modifier

// Целевой Java-вид класса описан в условии. Правьте только Registry, main не меняйте.
class Registry private constructor(val name: String) {
    private val entries = linkedMapOf<String, Int>()

    fun register(key: String, weight: Int = 1, tag: String = ""): Registry {
        entries[key + tag] = weight
        return this
    }

    fun save(): String = entries.entries.joinToString(",") { "${it.key}=${it.value}" }

    companion object {
        val DEFAULT = Registry("default")
        fun create(name: String): Registry = Registry(name)
        fun helper(): Int = 1
    }
}

private fun pub(m: Method) = Modifier.isPublic(m.modifiers) && !m.isSynthetic

fun main() {
    val c = Registry::class.java
    val n = readln().trim().toInt()
    repeat(n) {
        when (val cmd = readln().trim()) {
            "statics" -> println("statics: " + c.declaredMethods.filter { pub(it) && Modifier.isStatic(it.modifiers) }
                .map { "${it.name}/${it.parameterCount}" }.sorted().joinToString(" ").ifEmpty { "-" })
            "fields" -> println("fields: " + c.declaredFields.filter { Modifier.isStatic(it.modifiers) && Modifier.isPublic(it.modifiers) }
                .map { it.name }.sorted().joinToString(" ").ifEmpty { "-" })
            "overloads" -> println("overloads: " + c.declaredMethods.filter { pub(it) && it.name == "register" }
                .map { it.parameterCount }.sorted().joinToString(" ").ifEmpty { "-" })
            "throws" -> println("throws: " + c.getMethod("save").exceptionTypes.map { it.simpleName }.sorted().joinToString(" ").ifEmpty { "-" })
            "instance" -> println("instance: " + c.declaredMethods.filter { pub(it) && !Modifier.isStatic(it.modifiers) }
                .map { "${it.name}/${it.parameterCount}" }.sorted().joinToString(" ").ifEmpty { "-" })
            else -> println("unknown: $cmd")
        }
    }
}
