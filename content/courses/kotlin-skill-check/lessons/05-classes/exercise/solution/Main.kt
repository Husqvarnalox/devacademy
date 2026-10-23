sealed class Shape
data class Rect(val w: Int, val h: Int) : Shape()
data class Square(val side: Int) : Shape()
data class Triangle(val a: Int, val b: Int, val c: Int) : Shape()
object Empty : Shape()

fun perimeter(s: Shape): Int = when (s) {
    is Rect -> 2 * (s.w + s.h)
    is Square -> 4 * s.side
    is Triangle -> s.a + s.b + s.c
    Empty -> 0
}

fun describe(s: Shape): String = when (s) {
    is Rect -> "Rect ${s.w}x${s.h}"
    is Square -> "Square ${s.side}"
    is Triangle -> "Triangle ${s.a}-${s.b}-${s.c}"
    Empty -> "Empty"
}

fun parse(line: String): Shape {
    val p = line.split(" ")
    return when (p[0]) {
        "rect" -> Rect(p[1].toInt(), p[2].toInt())
        "square" -> Square(p[1].toInt())
        "triangle" -> Triangle(p[1].toInt(), p[2].toInt(), p[3].toInt())
        else -> Empty
    }
}

fun main() {
    val seen = mutableSetOf<Shape>()
    while (true) {
        val line = readlnOrNull() ?: break
        val s = parse(line)
        seen.add(s)
        println("${describe(s)}: ${perimeter(s)}")
    }
    println("Различных: ${seen.size}")
}
