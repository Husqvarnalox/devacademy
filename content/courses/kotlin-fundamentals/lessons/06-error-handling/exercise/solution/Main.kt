import kotlin.math.sqrt

class Machine {
    private val stack = ArrayDeque<Int>()

    fun snapshot(): List<Int> = stack.toList()

    fun exec(parts: List<String>): Int = when (parts[0]) {
        "push" -> {
            require(parts.size == 2) { "push needs one argument" }
            val x = parts[1].toIntOrNull() ?: throw NumberFormatException("bad number: ${parts[1]}")
            stack.addLast(x)
            x
        }
        "pop" -> {
            check(stack.isNotEmpty()) { "stack is empty" }
            stack.removeLast()
        }
        "div" -> {
            check(stack.size >= 2) { "need two operands" }
            val b = stack[stack.size - 1]
            val a = stack[stack.size - 2]
            val r = a / b
            stack.removeLast()
            stack.removeLast()
            stack.addLast(r)
            r
        }
        "sqrt" -> {
            check(stack.isNotEmpty()) { "stack is empty" }
            val x = stack.last()
            require(x >= 0) { "negative: $x" }
            stack.removeLast()
            val r = sqrt(x.toDouble()).toInt()
            stack.addLast(r)
            r
        }
        else -> throw IllegalArgumentException("unknown command: ${parts[0]}")
    }
}

fun main() {
    val machine = Machine()
    val n = readln().trim().toInt()
    repeat(n) {
        val parts = readln().trim().split(" ").filter { it.isNotEmpty() }
        val line = runCatching { machine.exec(parts) }.fold(
            onSuccess = { "ok $it" },
            onFailure = { "error ${it::class.simpleName}: ${it.message}" },
        )
        println(line)
    }
    println("stack=${machine.snapshot()}")
}
