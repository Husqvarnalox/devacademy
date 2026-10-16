data class Account(val id: String, val balance: Long)

sealed interface Command {
    data class Open(val id: String) : Command
    // TODO: Deposit, Withdraw, Transfer, Show

    companion object {
        // TODO: разберите строку; null, если команда неизвестна, число аргументов неверно
        // или сумма не является положительным целым
        fun parse(line: String): Command? = null
    }
}

object Bank {
    private val accounts = linkedMapOf<String, Account>()

    val total: Long get() = accounts.values.sumOf { it.balance }

    // TODO: when по sealed-типу без else; изменения счетов - через copy
    fun execute(c: Command): String = when (c) {
        is Command.Open -> "TODO"
    }
}

fun main() {
    val n = readln().trim().toInt()
    repeat(n) {
        val cmd = Command.parse(readln())
        println(if (cmd == null) "error: bad command" else Bank.execute(cmd))
    }
    println("total=${Bank.total}")
}
