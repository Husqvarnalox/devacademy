data class Account(val id: String, val balance: Long)

sealed interface Command {
    data class Open(val id: String) : Command
    data class Deposit(val id: String, val amount: Long) : Command
    data class Withdraw(val id: String, val amount: Long) : Command
    data class Transfer(val from: String, val to: String, val amount: Long) : Command
    data class Show(val id: String) : Command

    companion object {
        fun parse(line: String): Command? {
            val p = line.trim().split(" ").filter { it.isNotEmpty() }
            fun amount(s: String): Long? = s.toLongOrNull()?.takeIf { it > 0 }
            return when {
                p.size == 2 && p[0] == "open" -> Open(p[1])
                p.size == 2 && p[0] == "show" -> Show(p[1])
                p.size == 3 && p[0] == "deposit" -> amount(p[2])?.let { Deposit(p[1], it) }
                p.size == 3 && p[0] == "withdraw" -> amount(p[2])?.let { Withdraw(p[1], it) }
                p.size == 4 && p[0] == "transfer" -> amount(p[3])?.let { Transfer(p[1], p[2], it) }
                else -> null
            }
        }
    }
}

object Bank {
    private val accounts = linkedMapOf<String, Account>()

    val total: Long get() = accounts.values.sumOf { it.balance }

    fun execute(c: Command): String = when (c) {
        is Command.Open ->
            if (c.id in accounts) "error: exists ${c.id}"
            else { accounts[c.id] = Account(c.id, 0); "ok" }
        is Command.Deposit -> {
            val a = accounts[c.id]
            if (a == null) "error: no account ${c.id}"
            else { accounts[c.id] = a.copy(balance = a.balance + c.amount); "ok" }
        }
        is Command.Withdraw -> {
            val a = accounts[c.id]
            when {
                a == null -> "error: no account ${c.id}"
                a.balance < c.amount -> "error: insufficient ${c.id}"
                else -> { accounts[c.id] = a.copy(balance = a.balance - c.amount); "ok" }
            }
        }
        is Command.Transfer -> {
            val from = accounts[c.from]
            val to = accounts[c.to]
            when {
                from == null -> "error: no account ${c.from}"
                to == null -> "error: no account ${c.to}"
                from.balance < c.amount -> "error: insufficient ${c.from}"
                else -> {
                    accounts[c.from] = from.copy(balance = from.balance - c.amount)
                    val updatedTo = accounts.getValue(c.to)
                    accounts[c.to] = updatedTo.copy(balance = updatedTo.balance + c.amount)
                    "ok"
                }
            }
        }
        is Command.Show -> accounts[c.id]?.let { "${it.id}: ${it.balance}" } ?: "error: no account ${c.id}"
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
