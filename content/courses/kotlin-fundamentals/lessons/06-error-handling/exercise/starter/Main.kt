class Machine {
    private val stack = ArrayDeque<Int>()

    fun snapshot(): List<Int> = stack.toList()

    // TODO: проверяйте входные данные и состояние:
    //  push: require(2 части); число не разобралось -> NumberFormatException("bad number: X")
    //  pop, div, sqrt: check(...) для нехватки операндов; sqrt: require(x >= 0)
    //  неизвестная команда -> IllegalArgumentException("unknown command: X")
    // Неудачная команда не должна менять стек.
    fun exec(parts: List<String>): Int = when (parts[0]) {
        "push" -> { stack.addLast(parts[1].toInt()); stack.last() }
        "pop" -> stack.removeLast()
        "div" -> { val b = stack.removeLast(); val a = stack.removeLast(); (a / b).also { stack.addLast(it) } }
        "sqrt" -> { val x = stack.removeLast(); Math.sqrt(x.toDouble()).toInt().also { stack.addLast(it) } }
        else -> -1
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
