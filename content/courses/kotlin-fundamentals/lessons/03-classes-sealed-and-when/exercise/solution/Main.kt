sealed interface Expr {
    data class Num(val value: Long) : Expr
    data class Neg(val inner: Expr) : Expr
    data class Bin(val op: Char, val left: Expr, val right: Expr) : Expr
}

fun eval(e: Expr): Long = when (e) {
    is Expr.Num -> e.value
    is Expr.Neg -> -eval(e.inner)
    is Expr.Bin -> {
        val l = eval(e.left)
        val r = eval(e.right)
        when (e.op) {
            '+' -> l + r
            '-' -> l - r
            else -> l * r
        }
    }
}

fun show(e: Expr): String = when (e) {
    is Expr.Num -> e.value.toString()
    is Expr.Neg -> "-(${show(e.inner)})"
    is Expr.Bin -> "(${show(e.left)} ${e.op} ${show(e.right)})"
}

fun parse(tokens: Iterator<String>): Expr {
    val t = tokens.next()
    return when (t) {
        "+", "-", "*" -> {
            val left = parse(tokens)
            val right = parse(tokens)
            Expr.Bin(t[0], left, right)
        }
        "neg" -> Expr.Neg(parse(tokens))
        else -> Expr.Num(t.toLong())
    }
}

fun main() {
    val n = readln().trim().toInt()
    repeat(n) {
        val e = parse(readln().trim().split(" ").iterator())
        println("${show(e)} = ${eval(e)}")
    }
}
