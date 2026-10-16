// Выражения в префиксной записи: + 1 * 2 3 означает 1 + (2 * 3)
sealed interface Expr {
    data class Num(val value: Long) : Expr
    // TODO: добавьте Neg(inner) и Bin(op, left, right)
}

// TODO: when по sealed-типу должен охватывать все варианты, без else
fun eval(e: Expr): Long = when (e) {
    is Expr.Num -> e.value
}

// Num -> "5", Neg -> "-(x)", Bin -> "(l op r)"
fun show(e: Expr): String = when (e) {
    is Expr.Num -> e.value.toString()
}

fun parse(tokens: Iterator<String>): Expr {
    val t = tokens.next()
    // TODO: "+", "-", "*" читают два операнда рекурсивно; "neg" - один
    return Expr.Num(t.toLong())
}

fun main() {
    val n = readln().trim().toInt()
    repeat(n) {
        val e = parse(readln().trim().split(" ").iterator())
        println("${show(e)} = ${eval(e)}")
    }
}
