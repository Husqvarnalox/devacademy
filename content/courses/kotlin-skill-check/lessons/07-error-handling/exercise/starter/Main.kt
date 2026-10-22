// Возраст из строки: NumberFormatException для не числа, require для диапазона 0..120
fun parseAge(s: String): Int {
    return 0 // TODO
}

// parseAge внутри Result
fun safeParse(s: String): Result<Int> {
    return Result.success(0) // TODO
}

// Остаток после снятия: require(сумма > 0), check(баланс >= сумма)
fun withdraw(balance: Int, amount: Int): Int {
    return balance // TODO
}

fun main() {
    while (true) {
        val line = readlnOrNull() ?: break
        val parts = line.split(" ")
        val result = if (parts[0] == "age") {
            safeParse(parts.getOrElse(1) { "" })
        } else {
            runCatching { withdraw(parts[1].toInt(), parts[2].toInt()) }
        }
        println(result.fold({ "OK $it" }, { "ERR ${it::class.simpleName}: ${it.message}" }))
    }
}
