fun parseAge(s: String): Int {
    val age = s.trim().toInt()
    require(age in 0..120) { "Возраст вне диапазона 0..120: $age" }
    return age
}

fun safeParse(s: String): Result<Int> = runCatching { parseAge(s) }

fun withdraw(balance: Int, amount: Int): Int {
    require(amount > 0) { "Сумма должна быть положительной" }
    check(balance >= amount) { "Недостаточно средств: $balance < $amount" }
    return balance - amount
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
